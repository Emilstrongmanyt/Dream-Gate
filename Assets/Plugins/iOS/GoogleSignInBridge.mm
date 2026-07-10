#import <Foundation/Foundation.h>
#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>

static ASWebAuthenticationSession *dreamGateGoogleOAuthSession = nil;
static id dreamGateGoogleOAuthContext = nil;

@interface DreamGateGoogleOAuthContext : NSObject<ASWebAuthenticationPresentationContextProviding>
@property (nonatomic, copy) NSString *callbackObject;
@property (nonatomic, copy) NSString *callbackMethod;
@property (nonatomic, assign) BOOL didSendPayload;
@end

@implementation DreamGateGoogleOAuthContext

- (UIWindow *)activeWindow
{
    UIApplication *application = UIApplication.sharedApplication;

    if (@available(iOS 13.0, *))
    {
        for (UIScene *scene in application.connectedScenes)
        {
            if (![scene isKindOfClass:[UIWindowScene class]])
            {
                continue;
            }

            UIWindowScene *windowScene = (UIWindowScene *)scene;
            for (UIWindow *window in windowScene.windows)
            {
                if (window.isKeyWindow)
                {
                    return window;
                }
            }

            for (UIWindow *window in windowScene.windows)
            {
                if (window.rootViewController != nil)
                {
                    return window;
                }
            }
        }
    }

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    if (application.keyWindow != nil)
    {
        return application.keyWindow;
    }

    for (UIWindow *window in application.windows)
    {
        if (window.isKeyWindow || window.rootViewController != nil)
        {
            return window;
        }
    }
#pragma clang diagnostic pop

    return application.windows.firstObject;
}

- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession *)session API_AVAILABLE(ios(13.0))
{
    return [self activeWindow];
}

- (void)sendPayload:(NSDictionary *)payload
{
    if (self.didSendPayload)
    {
        return;
    }

    self.didSendPayload = YES;
    dreamGateGoogleOAuthContext = nil;
    dreamGateGoogleOAuthSession = nil;

    NSString *fallback = @"{\"success\":0,\"error\":\"Google sign in failed.\"}";
    if (self.callbackObject.length == 0 || self.callbackMethod.length == 0)
    {
        return;
    }

    NSError *error = nil;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    NSString *json = jsonData == nil
        ? fallback
        : [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    if (json == nil)
    {
        json = fallback;
    }

    UnitySendMessage(
        [self.callbackObject UTF8String],
        [self.callbackMethod UTF8String],
        [json UTF8String]);
}

@end

extern "C" void DreamGate_GoogleSignIn_Request(
    const char *authUrl,
    const char *callbackScheme,
    const char *callbackObject,
    const char *callbackMethod)
{
    if (@available(iOS 12.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            NSString *urlString = authUrl ? [NSString stringWithUTF8String:authUrl] : @"";
            NSString *scheme = callbackScheme ? [NSString stringWithUTF8String:callbackScheme] : @"";
            if (urlString.length == 0 || scheme.length == 0)
            {
                if (callbackObject != NULL && callbackMethod != NULL)
                {
                    UnitySendMessage(
                        callbackObject,
                        callbackMethod,
                        "{\"success\":0,\"error\":\"Google sign in is not configured.\"}");
                }
                return;
            }

            DreamGateGoogleOAuthContext *context = [DreamGateGoogleOAuthContext new];
            context.callbackObject = callbackObject ? [NSString stringWithUTF8String:callbackObject] : @"";
            context.callbackMethod = callbackMethod ? [NSString stringWithUTF8String:callbackMethod] : @"";
            context.didSendPayload = NO;
            dreamGateGoogleOAuthContext = context;

            dreamGateGoogleOAuthSession = [[ASWebAuthenticationSession alloc]
                initWithURL:[NSURL URLWithString:urlString]
                callbackURLScheme:scheme
                completionHandler:^(NSURL *callbackURL, NSError *error) {
                    if (context.didSendPayload)
                    {
                        return;
                    }

                    if (error != nil)
                    {
                        NSString *message = error.localizedDescription ?: @"Google sign in failed.";
                        if (error.code == ASWebAuthenticationSessionErrorCodeCanceledLogin)
                        {
                            message = @"Google sign in was cancelled.";
                        }

                        [context sendPayload:@{
                            @"success": @0,
                            @"error": message
                        }];
                        return;
                    }

                    if (callbackURL == nil)
                    {
                        [context sendPayload:@{
                            @"success": @0,
                            @"error": @"Google sign in did not return a callback URL."
                        }];
                        return;
                    }

                    [context sendPayload:@{
                        @"success": @1,
                        @"callbackUrl": callbackURL.absoluteString ?: @""
                    }];
                }];

            if (@available(iOS 13.0, *))
            {
                dreamGateGoogleOAuthSession.presentationContextProvider = context;
                dreamGateGoogleOAuthSession.prefersEphemeralWebBrowserSession = NO;
            }

            if (![dreamGateGoogleOAuthSession start])
            {
                [context sendPayload:@{
                    @"success": @0,
                    @"error": @"Could not start Google sign in."
                }];
                return;
            }

            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(120 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                if (dreamGateGoogleOAuthContext == context && !context.didSendPayload)
                {
                    [context sendPayload:@{
                        @"success": @0,
                        @"error": @"Google sign in timed out. Try again."
                    }];
                }
            });
        });
        return;
    }

    if (callbackObject != NULL && callbackMethod != NULL)
    {
        UnitySendMessage(
            callbackObject,
            callbackMethod,
            "{\"success\":0,\"error\":\"Google sign in requires iOS 12 or later.\"}");
    }
}