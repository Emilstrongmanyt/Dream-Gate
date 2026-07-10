#import <Foundation/Foundation.h>

static BOOL dreamGateHttpDone = NO;
static NSInteger dreamGateHttpStatusCode = 0;
static NSData *dreamGateHttpResponseBody = nil;
static NSString *dreamGateHttpTransportError = nil;

static void DreamGateHttpResetState(void)
{
    dreamGateHttpDone = NO;
    dreamGateHttpStatusCode = 0;
    dreamGateHttpResponseBody = nil;
    dreamGateHttpTransportError = nil;
}

static void DreamGateHttpFinish(NSInteger statusCode, NSData *body, NSString *error)
{
    dreamGateHttpStatusCode = statusCode;
    dreamGateHttpResponseBody = body;
    dreamGateHttpTransportError = error;
    dreamGateHttpDone = YES;
}

static NSMutableURLRequest *DreamGateHttpBuildRequest(
    NSString *urlString,
    NSString *method,
    NSString *body,
    NSString *apikey,
    NSString *authorization)
{
    NSURL *url = [NSURL URLWithString:urlString];
    if (url == nil)
    {
        return nil;
    }

    NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url];
    request.HTTPMethod = method;
    request.timeoutInterval = 45.0;
    [request setValue:@"application/json" forHTTPHeaderField:@"Accept"];
    [request setValue:@"identity" forHTTPHeaderField:@"Accept-Encoding"];

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

static void DreamGateHttpStart(NSString *urlString, NSString *method, NSString *body, NSString *apikey, NSString *authorization)
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

    NSURLSessionDataTask *task = [[NSURLSession sharedSession]
        dataTaskWithRequest:request
        completionHandler:^(NSData *data, NSURLResponse *response, NSError *error) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (error != nil)
                {
                    DreamGateHttpFinish(0, nil, error.localizedDescription ?: @"Native HTTP request failed.");
                    return;
                }

                NSInteger statusCode = 0;
                if ([response isKindOfClass:[NSHTTPURLResponse class]])
                {
                    statusCode = [(NSHTTPURLResponse *)response statusCode];
                }

                DreamGateHttpFinish(statusCode, data, nil);
            });
        }];
    [task resume];
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
        DreamGateHttpStart(
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
        DreamGateHttpStart(
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
    return dreamGateHttpResponseBody == nil ? 0 : (int)dreamGateHttpResponseBody.length;
}

extern "C" void DreamGate_Http_CopyBody(unsigned char *buffer, int bufferSize)
{
    if (buffer == NULL || bufferSize <= 0)
    {
        return;
    }

    buffer[0] = '\0';
    if (dreamGateHttpResponseBody == nil || dreamGateHttpResponseBody.length == 0)
    {
        return;
    }

    int copyLength = (int)dreamGateHttpResponseBody.length;
    if (copyLength > bufferSize)
    {
        copyLength = bufferSize;
    }

    memcpy(buffer, dreamGateHttpResponseBody.bytes, (size_t)copyLength);
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