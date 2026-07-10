#import <Foundation/Foundation.h>

static const int DreamGateHttpPluginRevision = 3;

static BOOL dreamGateHttpDone = NO;
static NSInteger dreamGateHttpStatusCode = 0;
static NSString *dreamGateHttpResponseBodyText = nil;
static NSString *dreamGateHttpTransportError = nil;

static void DreamGateHttpResetState(void)
{
    dreamGateHttpDone = NO;
    dreamGateHttpStatusCode = 0;
    dreamGateHttpResponseBodyText = nil;
    dreamGateHttpTransportError = nil;
}

static void DreamGateHttpFinish(NSInteger statusCode, NSData *body, NSString *error)
{
    dreamGateHttpStatusCode = statusCode;
    dreamGateHttpResponseBodyText = nil;

    if (body != nil && body.length > 0)
    {
        dreamGateHttpResponseBodyText = [[NSString alloc] initWithData:body encoding:NSUTF8StringEncoding];
        if (dreamGateHttpResponseBodyText == nil)
        {
            dreamGateHttpResponseBodyText = [[NSString alloc] initWithData:body encoding:NSISOLatin1StringEncoding];
        }
    }

    dreamGateHttpTransportError = error;
    dreamGateHttpDone = YES;
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
    DreamGateHttpResetState();

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

            NSInteger statusCode = httpResponse != nil ? httpResponse.statusCode : 0;
            NSString *transportError = requestError != nil ? requestError.localizedDescription : nil;
            NSData *bodyData = responseData;

            dispatch_async(dispatch_get_main_queue(), ^{
                if (transportError != nil)
                {
                    DreamGateHttpFinish(0, nil, transportError);
                    return;
                }

                DreamGateHttpFinish(statusCode, bodyData, nil);
            });
        }
    });
}

extern "C" int DreamGate_Http_GetRevision(void)
{
    return DreamGateHttpPluginRevision;
}

extern "C" void DreamGate_Http_Reset(void)
{
    DreamGateHttpResetState();
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
    return dreamGateHttpDone ? 1 : 0;
}

extern "C" int DreamGate_Http_GetStatusCode(void)
{
    return (int)dreamGateHttpStatusCode;
}

extern "C" int DreamGate_Http_GetBodySize(void)
{
    if (dreamGateHttpResponseBodyText == nil || dreamGateHttpResponseBodyText.length == 0)
    {
        return 0;
    }

    const char *utf8 = [dreamGateHttpResponseBodyText UTF8String];
    return utf8 == NULL ? 0 : (int)strlen(utf8);
}

extern "C" void DreamGate_Http_CopyBody(char *buffer, int bufferSize)
{
    if (buffer == NULL || bufferSize <= 0)
    {
        return;
    }

    buffer[0] = '\0';
    if (dreamGateHttpResponseBodyText == nil || dreamGateHttpResponseBodyText.length == 0)
    {
        return;
    }

    const char *utf8 = [dreamGateHttpResponseBodyText UTF8String];
    if (utf8 == NULL)
    {
        return;
    }

    strncpy(buffer, utf8, (size_t)bufferSize - 1);
    buffer[bufferSize - 1] = '\0';
}

extern "C" void DreamGate_Http_CopyError(char *buffer, int bufferSize)
{
    if (buffer == NULL || bufferSize <= 0)
    {
        return;
    }

    buffer[0] = '\0';
    if (dreamGateHttpTransportError == nil || dreamGateHttpTransportError.length == 0)
    {
        return;
    }

    const char *utf8 = [dreamGateHttpTransportError UTF8String];
    if (utf8 == NULL)
    {
        return;
    }

    strncpy(buffer, utf8, (size_t)bufferSize - 1);
    buffer[bufferSize - 1] = '\0';
}