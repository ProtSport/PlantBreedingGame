package com.protsport.plantbreeding;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import java.util.concurrent.Executors;

/**
 * Вхід через Google на Android (Credential Manager) для прив'язки прогресу до акаунта.
 * Повертає Google ID token у Unity: UnitySendMessage(receiver, "OnGoogleSignInResult",
 * "ok|<token>" / "cancel|" / "err|<опис>"). Далі Firebase Auth робить з нього
 * GoogleAuthProvider-credential (Cloud/GoogleSignInBridge.cs, CloudService).
 */
public class GoogleSignInBridge {
    public static void signIn(final Activity activity, final String webClientId, final String receiver) {
        try {
            GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(webClientId).build();
            GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
            CredentialManager manager = CredentialManager.create(activity);
            manager.getCredentialAsync(activity, request, new CancellationSignal(), Executors.newSingleThreadExecutor(),
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse result) {
                        try {
                            Credential credential = result.getCredential();
                            if (credential instanceof CustomCredential
                                    && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                GoogleIdTokenCredential google =
                                    GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData());
                                send(receiver, "ok|" + google.getIdToken());
                            } else {
                                send(receiver, "err|unexpected credential " + credential.getType());
                            }
                        } catch (Exception e) {
                            send(receiver, "err|" + e);
                        }
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        if (e instanceof GetCredentialCancellationException) send(receiver, "cancel|");
                        else send(receiver, "err|" + e.getClass().getSimpleName() + ": " + e.getMessage());
                    }
                });
        } catch (Throwable t) {
            send(receiver, "err|" + t);
        }
    }

    private static void send(String receiver, String message) {
        UnityPlayer.UnitySendMessage(receiver, "OnGoogleSignInResult", message);
    }
}
