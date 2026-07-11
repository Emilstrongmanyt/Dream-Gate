#import <Foundation/Foundation.h>
#import <os/lock.h>

static const int DreamGateHttpPluginRevision = 4;

static os_unfair_lock dreamGateHttpLock = OS_UNFAIR_LOCK_INIT;
static BOOL dreamGateHttpDone = NO;
static NSInteger dreamGateHttpStatusCode = 0;
static NSData *dreamGateHttpResponseBodyData = nil;
static NSString *dreamGateHttpTransportError = nil;
static int dreamGateHttpGeneration = 0;

static void DreamGateHttpResetStateLocked(void)
{
    dreamGateHttpDone = NO;
    dreamGateHttpStatusCode = 0;
    dreamGateHttpResponseBodyData = nil;
    dreamGateHttpTransportError = nil;
}

static void DreamGateHttpResetState(void)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    DreamGateHttpResetStateLocked();
    os_unfair_lock_unlock(&dreamGateHttpLock);
}

static void DreamGateHttpFinish(NSInteger statusCode, NSData *body, NSString *error)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    dreamGateHttpStatusCode = statusCode;
    dreamGateHttpResponseBodyData = body != nil ? [body copy] : nil;
    dreamGateHttpTransportError = error != nil ? [error copy] : nil;
    dreamGateHttpDone = YES;
    os_unfair_lock_unlock(&dreamGateHttpLock);
}

static NSString *DreamGateHttpUrlWithApiKey(NSString *urlString, NSString *apikey)
{
    if (urlString.length == 0 || apikey.length == 0)
    {
        return urlString;
    }

    if ([urlString rangeOfString:@"apikey=" options:NSCaseInsensitiveSearch].location != NSNotFound)
    {
        return urlString;
    }

    NSString *encodedKey = [apikey stringByAddingPercentEncodingWithAllowedCharacters:[NSCharacterSet URLQueryAllowedCharacterSet]];
    if (encodedKey.length == 0)
    {
        return urlString;
    }

    NSString *separator = [urlString containsString:@"?"] ? @"&" : @"?";
    return [NSString stringWithFormat:@"%@%@apikey=%@", urlString, separator, encodedKey];
}

static NSMutableURLRequest *DreamGateHttpBuildRequest(
    NSString *urlString,
    NSString *method,
    NSString *body,
    NSString *apikey,
    NSString *authorization)
{
    NSString *resolvedUrlString = DreamGateHttpUrlWithApiKey(urlString, apikey);
    NSURL *url = [NSURL URLWithString:resolvedUrlString];
    if (url == nil)
    {
        return nil;
    }

    NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url];
    request.HTTPMethod = method;
    request.timeoutInterval = 45.0;
    request.cachePolicy = NSURLRequestReloadIgnoringLocalCacheData;
    [request setValue:@"application/json" forHTTPHeaderField:@"Accept"];
    [request setValue:@"identity" forHTTPHeaderField:@"Accept-Encoding"];
    [request setValue:@"close" forHTTPHeaderField:@"Connection"];

    if (apikey.length > 0)
    {
        [request setValue:apikey forHTTPHeaderField:@"apikey"];
    }

    if (authorization.length > 0)
    {
        [request setValue:authorization forHTTPHeaderField:@"Authorization"];
    }

    if ([method isEqualToString:@"POST"])
    {
        NSData *bodyData = body == nil ? [NSData data] : [body dataUsingEncoding:NSUTF8StringEncoding];
        request.HTTPBody = bodyData;
        [request setValue:@"application/json" forHTTPHeaderField:@"Content-Type"];
        [request setValue:[NSString stringWithFormat:@"%lu", (unsigned long)bodyData.length]
            forHTTPHeaderField:@"Content-Length"];
    }

    return request;
}

static void DreamGateHttpExecute(
    NSString *urlString,
    NSString *method,
    NSString *body,
    NSString *apikey,
    NSString *authorization)
{
    int generation = 0;

    os_unfair_lock_lock(&dreamGateHttpLock);
    dreamGateHttpGeneration += 1;
    generation = dreamGateHttpGeneration;
    DreamGateHttpResetStateLocked();
    os_unfair_lock_unlock(&dreamGateHttpLock);

    if (urlString.length == 0)
    {
        DreamGateHttpFinish(0, nil, @"Request URL is missing.");
        return;
    }

    NSMutableURLRequest *request = DreamGateHttpBuildRequest(urlString, method, body, apikey, authorization);
    if (request == nil)
    {
        DreamGateHttpFinish(0, nil, @"Invalid request URL.");
        return;
    }

    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        @autoreleasepool
        {
            __block NSData *responseData = nil;
            __block NSHTTPURLResponse *httpResponse = nil;
            __block NSError *requestError = nil;
            dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);

            NSURLSessionConfiguration *configuration = [NSURLSessionConfiguration ephemeralSessionConfiguration];
            configuration.timeoutIntervalForRequest = 45.0;
            configuration.timeoutIntervalForResource = 45.0;
            configuration.HTTPShouldUsePipelining = NO;
            configuration.waitsForConnectivity = YES;
            configuration.requestCachePolicy = NSURLRequestReloadIgnoringLocalCacheData;
            NSURLSession *session = [NSURLSession sessionWithConfiguration:configuration];

            NSURLSessionDataTask *task = [session
                dataTaskWithRequest:request
                completionHandler:^(NSData *data, NSURLResponse *response, NSError *error) {
                    responseData = data;
                    requestError = error;
                    if ([response isKindOfClass:[NSHTTPURLResponse class]])
                    {
                        httpResponse = (NSHTTPURLResponse *)response;
                    }
                    dispatch_semaphore_signal(semaphore);
                }];
            [task resume];
            dispatch_semaphore_wait(semaphore, dispatch_time(DISPATCH_TIME_NOW, (int64_t)(45 * NSEC_PER_SEC)));
            [session finishTasksAndInvalidate];

            os_unfair_lock_lock(&dreamGateHttpLock);
            BOOL stale = generation != dreamGateHttpGeneration;
            os_unfair_lock_unlock(&dreamGateHttpLock);
            if (stale)
            {
                return;
            }

            NSInteger statusCode = httpResponse != nil ? httpResponse.statusCode : 0;
            NSString *transportError = requestError != nil ? requestError.localizedDescription : nil;
            NSData *bodyData = responseData;

            if (transportError != nil)
            {
                DreamGateHttpFinish(0, nil, transportError);
                return;
            }

            DreamGateHttpFinish(statusCode, bodyData, nil);
        }
    });
}

extern "C" int DreamGate_Http_GetRevision(void)
{
    return DreamGateHttpPluginRevision;
}

extern "C" void DreamGate_Http_Reset(void)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    dreamGateHttpGeneration += 1;
    DreamGateHttpResetStateLocked();
    os_unfair_lock_unlock(&dreamGateHttpLock);
}

extern "C" void DreamGate_Http_StartPost(
    const char *url,
    const char *body,
    const char *apikey,
    const char *authorization)
{
    @autoreleasepool
    {
        DreamGateHttpExecute(
            url ? [NSString stringWithUTF8String:url] : @"",
            @"POST",
            body ? [NSString stringWithUTF8String:body] : @"",
            apikey ? [NSString stringWithUTF8String:apikey] : @"",
            authorization ? [NSString stringWithUTF8String:authorization] : @"");
    }
}

extern "C" void DreamGate_Http_StartGet(
    const char *url,
    const char *apikey,
    const char *authorization)
{
    @autoreleasepool
    {
        DreamGateHttpExecute(
            url ? [NSString stringWithUTF8String:url] : @"",
            @"GET",
            nil,
            apikey ? [NSString stringWithUTF8String:apikey] : @"",
            authorization ? [NSString stringWithUTF8String:authorization] : @"");
    }
}

extern "C" int DreamGate_Http_IsDone(void)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    BOOL done = dreamGateHttpDone;
    os_unfair_lock_unlock(&dreamGateHttpLock);
    return done ? 1 : 0;
}

extern "C" int DreamGate_Http_GetStatusCode(void)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    int statusCode = (int)dreamGateHttpStatusCode;
    os_unfair_lock_unlock(&dreamGateHttpLock);
    return statusCode;
}

extern "C" int DreamGate_Http_GetBodyByteCount(void)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    NSUInteger length = dreamGateHttpResponseBodyData != nil ? dreamGateHttpResponseBodyData.length : 0;
    os_unfair_lock_unlock(&dreamGateHttpLock);
    return (int)length;
}

extern "C" int DreamGate_Http_CopyBody(void *buffer, int bufferSize)
{
    if (buffer == NULL || bufferSize <= 0)
    {
        return 0;
    }

    os_unfair_lock_lock(&dreamGateHttpLock);
    NSData *bodyData = dreamGateHttpResponseBodyData;
    NSUInteger length = bodyData != nil ? bodyData.length : 0;
    if (length == 0)
    {
        os_unfair_lock_unlock(&dreamGateHttpLock);
        return 0;
    }

    NSUInteger copyLength = length;
    if (copyLength > (NSUInteger)bufferSize)
    {
        copyLength = (NSUInteger)bufferSize;
    }

    memcpy(buffer, bodyData.bytes, copyLength);
    os_unfair_lock_unlock(&dreamGateHttpLock);
    return (int)copyLength;
}

extern "C" void DreamGate_Http_CopyError(char *buffer, int bufferSize)
{
    if (buffer == NULL || bufferSize <= 0)
    {
        return;
    }

    buffer[0] = '\0';

    os_unfair_lock_lock(&dreamGateHttpLock);
    NSString *error = dreamGateHttpTransportError;
    os_unfair_lock_unlock(&dreamGateHttpLock);

    if (error == nil || error.length == 0)
    {
        return;
    }

    const char *utf8 = [error UTF8String];
    if (utf8 == NULL)
    {
        return;
    }

    strncpy(buffer, utf8, (size_t)bufferSize - 1);
    buffer[bufferSize - 1] = '\0';
}