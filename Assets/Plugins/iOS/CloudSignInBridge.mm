// Вхід через Google і Apple на iOS для прив'язки прогресу до акаунта (Cloud/CloudService).
// Відповідь у Unity: UnitySendMessage(receiver, метод, "ok|…" / "cancel|" / "err|опис"):
//   Google → "ok|<idToken>|<accessToken>", Apple → "ok|<identityToken>".
// Firebase Auth робить з токенів credential (GoogleAuthProvider / OAuthProvider "apple.com").

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>
#import <GoogleSignIn/GoogleSignIn.h>
#import "AppDelegateListener.h"

extern "C" UIViewController* UnityGetGLViewController();
extern "C" UIWindow* UnityGetMainWindow();
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static void PBSend(NSString* receiver, const char* method, NSString* message)
{
    UnitySendMessage(receiver.UTF8String, method, message.UTF8String);
}

// ── Google ────────────────────────────────────────────────────────────────
// Повернення з браузера/застосунку Google за URL-схемою REVERSED_CLIENT_ID
// (додається в Info.plist: Editor/IosSignInPostprocess.cs).
@interface PBGoogleUrlListener : NSObject<AppDelegateListener>
@end

@implementation PBGoogleUrlListener
+ (void)load
{
    static PBGoogleUrlListener* instance;
    instance = [PBGoogleUrlListener new];
    UnityRegisterAppDelegateListener(instance);
}

- (void)onOpenURL:(NSNotification*)notification
{
    NSURL* url = notification.userInfo[@"url"];
    if (url != nil) [GIDSignIn.sharedInstance handleURL:url];
}
@end

extern "C" void PBGoogleSignIn(const char* clientId, const char* receiver)
{
    NSString* client = [NSString stringWithUTF8String:clientId];
    NSString* target = [NSString stringWithUTF8String:receiver];
    dispatch_async(dispatch_get_main_queue(), ^{
        GIDSignIn.sharedInstance.configuration = [[GIDConfiguration alloc] initWithClientID:client];
        [GIDSignIn.sharedInstance signInWithPresentingViewController:UnityGetGLViewController()
                                                         completion:^(GIDSignInResult* result, NSError* error) {
            if (error != nil)
            {
                if (error.code == kGIDSignInErrorCodeCanceled) PBSend(target, "OnGoogleSignInResult", @"cancel|");
                else PBSend(target, "OnGoogleSignInResult", [@"err|" stringByAppendingString:error.localizedDescription]);
                return;
            }
            NSString* idToken = result.user.idToken.tokenString ?: @"";
            NSString* accessToken = result.user.accessToken.tokenString ?: @"";
            PBSend(target, "OnGoogleSignInResult", [NSString stringWithFormat:@"ok|%@|%@", idToken, accessToken]);
        }];
    });
}

// ── Apple ─────────────────────────────────────────────────────────────────
@interface PBAppleSignIn : NSObject<ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property(nonatomic, copy) NSString* receiver;
@end

static PBAppleSignIn* gAppleSignIn; // тримаємо, поки триває вхід

@implementation PBAppleSignIn
- (void)authorizationController:(ASAuthorizationController*)controller
   didCompleteWithAuthorization:(ASAuthorization*)authorization
{
    NSString* message = @"err|unexpected credential";
    if ([authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]])
    {
        ASAuthorizationAppleIDCredential* credential = (ASAuthorizationAppleIDCredential*)authorization.credential;
        NSString* token = credential.identityToken != nil
            ? [[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding] : nil;
        message = token.length > 0 ? [@"ok|" stringByAppendingString:token] : @"err|no identity token";
    }
    PBSend(self.receiver, "OnAppleSignInResult", message);
    gAppleSignIn = nil;
}

- (void)authorizationController:(ASAuthorizationController*)controller didCompleteWithError:(NSError*)error
{
    if (error.code == ASAuthorizationErrorCanceled) PBSend(self.receiver, "OnAppleSignInResult", @"cancel|");
    else PBSend(self.receiver, "OnAppleSignInResult", [@"err|" stringByAppendingString:error.localizedDescription]);
    gAppleSignIn = nil;
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController*)controller
{
    return UnityGetMainWindow();
}
@end

extern "C" void PBAppleSignIn(const char* hashedNonce, const char* receiver)
{
    NSString* nonce = [NSString stringWithUTF8String:hashedNonce];
    NSString* target = [NSString stringWithUTF8String:receiver];
    dispatch_async(dispatch_get_main_queue(), ^{
        ASAuthorizationAppleIDRequest* request = [[ASAuthorizationAppleIDProvider new] createRequest];
        request.requestedScopes = @[ASAuthorizationScopeEmail];
        request.nonce = nonce; // SHA-256 від rawNonce; сам rawNonce іде у Firebase
        gAppleSignIn = [PBAppleSignIn new];
        gAppleSignIn.receiver = target;
        ASAuthorizationController* controller = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        controller.delegate = gAppleSignIn;
        controller.presentationContextProvider = gAppleSignIn;
        [controller performRequests];
    });
}
