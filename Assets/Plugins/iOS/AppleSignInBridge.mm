#import <Foundation/Foundation.h>
#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController(void);

static id dreamGateAppleSignInDelegate = nil;
static ASAuthorizationController *dreamGateAppleAuthorizationController = nil;
static NSString *dreamGateApplePendingJson = nil;

@interface DreamGateAppleSignInDelegate : NSObject<ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property (nonatomic, copy) NSString *callbackObject;
@property (nonatomic, copy) NSString *callbackMethod;
@property (nonatomic, assign) BOOL didSendPayload;
@end

@implementation DreamGateAppleSignInDelegate

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

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller
{
    UIViewController *unityController = UnityGetGLViewController();
    if (unityController != nil)
    {
        if (unityController.view.window != nil)
        {
            return unityController.view.window;
        }

        if (unityController.view != nil)
        {
            return unityController.view;
        }
    }

    UIWindow *window = [self activeWindow];
    if (window != nil)
    {
        return window;
    }

    return UIApplication.sharedApplication.windows.firstObject;
}

- (void)sendPayload:(NSDictionary *)payload
{
    if (self.didSendPayload)
    {
        return;
    }

    self.didSendPayload = YES;
    dreamGateAppleSignInDelegate = nil;
    dreamGateAppleAuthorizationController = nil;

    NSString *fallback = @"{\"success\":0,\"error\":\"Apple sign in failed.\"}";
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

    dreamGateApplePendingJson = json;
    UnitySendMessage(
        [self.callbackObject UTF8String],
        [self.callbackMethod UTF8String],
        "ready");
}

- (void)authorizationController:(ASAuthorizationController *)controller
   didCompleteWithAuthorization:(ASAuthorization *)authorization API_AVAILABLE(ios(13.0))
{
    if (![authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]])
    {
        [self sendPayload:@{
            @"success": @0,
            @"error": @"Unsupported Apple credential type."
        }];
        return;
    }

    ASAuthorizationAppleIDCredential *credential = (ASAuthorizationAppleIDCredential *)authorization.credential;
    NSString *identityToken = credential.identityToken == nil
        ? @""
        : [[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding];

    [self sendPayload:@{
        @"success": identityToken.length > 0 ? @1 : @0,
        @"identityToken": identityToken ?: @"",
        @"email": credential.email ?: @"",
        @"givenName": credential.fullName.givenName ?: @"",
        @"familyName": credential.fullName.familyName ?: @"",
        @"error": identityToken.length > 0 ? @"" : @"Apple did not return an identity token."
    }];
}

- (void)authorizationController:(ASAuthorizationController *)controller
           didCompleteWithError:(NSError *)error API_AVAILABLE(ios(13.0))
{
    NSString *message = error.localizedDescription ?: @"Apple sign in failed.";
    if (error.code == ASAuthorizationErrorCanceled)
    {
        message = @"Apple sign in was cancelled.";
    }

    [self sendPayload:@{
        @"success": @0,
        @"error": message
    }];
}

@end

extern "C" void DreamGate_AppleSignIn_CopyResult(char *buffer, int bufferSize)
{
    if (buffer == NULL || bufferSize <= 0)
    {
        return;
    }

    buffer[0] = '\0';
    if (dreamGateApplePendingJson == nil || dreamGateApplePendingJson.length == 0)
    {
        return;
    }

    const char *utf8 = [dreamGateApplePendingJson UTF8String];
    if (utf8 == NULL)
    {
        return;
    }

    strncpy(buffer, utf8, (size_t)bufferSize - 1);
    buffer[bufferSize - 1] = '\0';
    dreamGateApplePendingJson = nil;
}

extern "C" void DreamGate_AppleSignIn_Request(const char *hashedNonce, const char *callbackObject, const char *callbackMethod)
{
    if (@available(iOS 13.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            DreamGateAppleSignInDelegate *delegate = [DreamGateAppleSignInDelegate new];
            delegate.callbackObject = callbackObject ? [NSString stringWithUTF8String:callbackObject] : @"";
            delegate.callbackMethod = callbackMethod ? [NSString stringWithUTF8String:callbackMethod] : @"";
            delegate.didSendPayload = NO;
            dreamGateAppleSignInDelegate = delegate;

            ASAuthorizationAppleIDProvider *provider = [ASAuthorizationAppleIDProvider new];
            ASAuthorizationAppleIDRequest *request = [provider createRequest];
            request.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];

            if (hashedNonce != NULL)
            {
                NSString *nonce = [NSString stringWithUTF8String:hashedNonce];
                if (nonce.length > 0)
                {
                    request.nonce = nonce;
                }
            }

            ASAuthorizationController *controller =
                [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
            controller.delegate = delegate;
            controller.presentationContextProvider = delegate;
            dreamGateAppleAuthorizationController = controller;
            [controller performRequests];

            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(60 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                if (dreamGateAppleSignInDelegate == delegate && !delegate.didSendPayload)
                {
                    [delegate sendPayload:@{
                        @"success": @0,
                        @"error": @"Apple sign in timed out. Try again."
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
            "{\"success\":0,\"error\":\"Sign in with Apple requires iOS 13 or later.\"}");
    }
}