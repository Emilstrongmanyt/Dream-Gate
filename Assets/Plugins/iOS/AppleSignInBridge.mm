#import <Foundation/Foundation.h>
#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>

static id dreamGateAppleSignInDelegate = nil;

@interface DreamGateAppleSignInDelegate : NSObject<ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property (nonatomic, copy) NSString *callbackObject;
@property (nonatomic, copy) NSString *callbackMethod;
@end

@implementation DreamGateAppleSignInDelegate

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller
{
    if (@available(iOS 13.0, *))
    {
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
        {
            if (scene.activationState != UISceneActivationStateForegroundActive ||
                ![scene isKindOfClass:[UIWindowScene class]])
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
        }
    }

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    return UIApplication.sharedApplication.keyWindow;
#pragma clang diagnostic pop
}

- (void)sendPayload:(NSDictionary *)payload
{
    NSError *error = nil;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    if (error != nil || jsonData == nil)
    {
        return;
    }

    NSString *json = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    if (json == nil || self.callbackObject.length == 0 || self.callbackMethod.length == 0)
    {
        return;
    }

    UnitySendMessage(
        [self.callbackObject UTF8String],
        [self.callbackMethod UTF8String],
        [json UTF8String]);
    dreamGateAppleSignInDelegate = nil;
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

    NSMutableDictionary *payload = [@{
        @"success": identityToken.length > 0 ? @1 : @0,
        @"identityToken": identityToken ?: @"",
        @"email": credential.email ?: @"",
        @"givenName": credential.fullName.givenName ?: @"",
        @"familyName": credential.fullName.familyName ?: @"",
        @"error": identityToken.length > 0 ? @"" : @"Apple did not return an identity token."
    } mutableCopy];

    [self sendPayload:payload];
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

extern "C" void DreamGate_AppleSignIn_Request(const char *hashedNonce, const char *callbackObject, const char *callbackMethod)
{
    if (@available(iOS 13.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            DreamGateAppleSignInDelegate *delegate = [DreamGateAppleSignInDelegate new];
            delegate.callbackObject = callbackObject ? [NSString stringWithUTF8String:callbackObject] : @"";
            delegate.callbackMethod = callbackMethod ? [NSString stringWithUTF8String:callbackMethod] : @"";
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
            [controller performRequests];
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