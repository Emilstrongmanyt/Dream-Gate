#import <Foundation/Foundation.h>
#import <os/lock.h>

static const int DreamGateHttpPluginRevision = 6;
static NSString *const DreamGateHttpBodyFileName = @"dreamgate_auth_response.bin";

static os_unfair_lock dreamGateHttpLock = OS_UNFAIR_LOCK_INIT;
static BOOL dreamGateHttpDone = NO;
static NSInteger dreamGateHttpStatusCode = 0;
static NSData *dreamGateHttpResponseBodyData = nil;
static NSString *dreamGateHttpResponseFilePath = nil;
static NSString *dreamGateHttpTransportError = nil;
static int dreamGateHttpNativeByteCount = 0;
static int dreamGateHttpGeneration = 0;

static void DreamGateHttpResetStateLocked(void)
{
    dreamGateHttpDone = NO;
    dreamGateHttpStatusCode = 0;
    dreamGateHttpResponseBodyData = nil;
    dreamGateHttpResponseFilePath = nil;
    dreamGateHttpTransportError = nil;
    dreamGateHttpNativeByteCount = 0;
}

static void DreamGateHttpResetState(void)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    DreamGateHttpResetStateLocked();
    os_unfair_lock_unlock(&dreamGateHttpLock);
}

static NSString *DreamGateHttpBodyFilePath(void)
{
    return [NSTemporaryDirectory() stringByAppendingPathComponent:DreamGateHttpBodyFileName];
}

static void DreamGateHttpWriteBodyFile(NSData *body)
{
    if (body == nil || body.length == 0)
    {
        return;
    }

    NSString *path = DreamGateHttpBodyFilePath();
    [body writeToFile:path atomically:YES];
    dreamGateHttpResponseFilePath = path;
}

static NSData *DreamGateHttpSendSynchronously(NSURLRequest *request, NSInteger *statusCode)
{
    if (statusCode != NULL)
    {
        *statusCode = 0;
    }

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    NSURLResponse *response = nil;
    NSError *error = nil;
    NSData *data = [NSURLConnection sendSynchronousRequest:request returningResponse:&response error:&error];
#pragma clang diagnostic pop

    if (error != nil)
    {
        return nil;
    }

    if ([response isKindOfClass:[NSHTTPURLResponse class]] && statusCode != NULL)
    {
        *statusCode = ((NSHTTPURLResponse *)response).statusCode;
    }

    return data;
}

static void DreamGateHttpFinish(NSInteger statusCode, NSData *body, NSString *error)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    dreamGateHttpStatusCode = statusCode;
    dreamGateHttpResponseBodyData = body != nil ? [body copy] : nil;
    dreamGateHttpNativeByteCount = body != nil ? (int)body.length : 0;
    dreamGateHttpResponseFilePath = nil;
    dreamGateHttpTransportError = error != nil ? [error copy] : nil;
    DreamGateHttpWriteBodyFile(body);
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

            if ((bodyData == nil || bodyData.length == 0) && statusCode >= 200 && statusCode < 300)
            {
                NSInteger syncStatusCode = 0;
                NSData *syncData = DreamGateHttpSendSynchronously(request, &syncStatusCode);
                if (syncData != nil && syncData.length > 0)
                {
                    bodyData = syncData;
                    if (syncStatusCode > 0)
                    {
                        statusCode = syncStatusCode;
                    }
                }
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
    int byteCount = dreamGateHttpNativeByteCount;
    os_unfair_lock_unlock(&dreamGateHttpLock);
    return byteCount;
}

extern "C" int DreamGate_Http_HasBodyFile(void)
{
    os_unfair_lock_lock(&dreamGateHttpLock);
    BOOL hasFile = dreamGateHttpResponseFilePath != nil && dreamGateHttpResponseFilePath.length > 0;
    os_unfair_lock_unlock(&dreamGateHttpLock);
    return hasFile ? 1 : 0;
}

extern "C" int DreamGate_Http_CopyBodyFilePath(char *buffer, int bufferSize)
{
    if (buffer == NULL || bufferSize <= 0)
    {
        return 0;
    }

    buffer[0] = '\0';

    os_unfair_lock_lock(&dreamGateHttpLock);
    NSString *path = dreamGateHttpResponseFilePath;
    os_unfair_lock_unlock(&dreamGateHttpLock);

    if (path == nil || path.length == 0)
    {
        return 0;
    }

    const char *utf8 = [path UTF8String];
    if (utf8 == NULL)
    {
        return 0;
    }

    strncpy(buffer, utf8, (size_t)bufferSize - 1);
    buffer[bufferSize - 1] = '\0';
    return (int)strlen(buffer);
}

extern "C" int DreamGate_Http_CopyBody(unsigned char *buffer, int bufferSize)
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

static void DreamGateAuthHttpSendMessage(
    const char *callbackObject,
    const char *callbackMethod,
    NSDictionary *payload)
{
    if (callbackObject == NULL || callbackMethod == NULL)
    {
        return;
    }

    NSError *error = nil;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    NSString *fallback = @"{\"ok\":0,\"status\":0,\"error\":\"Native auth callback failed.\"}";
    NSString *json = jsonData == nil
        ? fallback
        : [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    if (json == nil)
    {
        json = fallback;
    }

    UnitySendMessage(callbackObject, callbackMethod, [json UTF8String]);
}

static void DreamGateAuthHttpExecutePost(
    NSString *urlString,
    NSString *body,
    NSString *apikey,
    NSString *authorization,
    const char *callbackObject,
    const char *callbackMethod)
{
    if (urlString.length == 0)
    {
        DreamGateAuthHttpSendMessage(callbackObject, callbackMethod, @{
            @"ok": @0,
            @"status": @0,
            @"error": @"Request URL is missing."
        });
        return;
    }

    NSMutableURLRequest *request = DreamGateHttpBuildRequest(urlString, @"POST", body, apikey, authorization);
    if (request == nil)
    {
        DreamGateAuthHttpSendMessage(callbackObject, callbackMethod, @{
            @"ok": @0,
            @"status": @0,
            @"error": @"Invalid request URL."
        });
        return;
    }

    NSInteger statusCode = 0;
    NSString *transportError = nil;
    NSData *responseData = DreamGateHttpSendSynchronously(request, &statusCode);

    if (responseData == nil || responseData.length == 0)
    {
        __block NSData *sessionData = nil;
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
                sessionData = data;
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

        if (requestError != nil)
        {
            transportError = requestError.localizedDescription;
        }
        else
        {
            responseData = sessionData;
            if (httpResponse != nil)
            {
                statusCode = httpResponse.statusCode;
            }
        }
    }

    if (transportError != nil)
    {
        DreamGateAuthHttpSendMessage(callbackObject, callbackMethod, @{
            @"ok": @0,
            @"status": @0,
            @"error": transportError
        });
        return;
    }

    NSString *bodyB64 = @"";
    if (responseData != nil && responseData.length > 0)
    {
        bodyB64 = [responseData base64EncodedStringWithOptions:0];
    }

    DreamGateAuthHttpSendMessage(callbackObject, callbackMethod, @{
        @"ok": @1,
        @"status": @(statusCode),
        @"bodyB64": bodyB64 ?: @""
    });
}

extern "C" void DreamGate_AuthHttp_StartPost(
    const char *url,
    const char *body,
    const char *apikey,
    const char *authorization,
    const char *callbackObject,
    const char *callbackMethod)
{
    @autoreleasepool
    {
        dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
            @autoreleasepool
            {
                DreamGateAuthHttpExecutePost(
                    url ? [NSString stringWithUTF8String:url] : @"",
                    body ? [NSString stringWithUTF8String:body] : @"",
                    apikey ? [NSString stringWithUTF8String:apikey] : @"",
                    authorization ? [NSString stringWithUTF8String:authorization] : @"",
                    callbackObject,
                    callbackMethod);
            }
        });
    }
}