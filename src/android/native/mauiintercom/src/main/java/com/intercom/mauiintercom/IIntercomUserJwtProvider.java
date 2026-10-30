package com.intercom.mauiintercom;

/**
 * Asked by Intercom for a fresh user JWT when the current one is about to expire or has
 * expired.
 *
 * <p>Mirrors io.intercom.android.sdk.IntercomUserJwtProvider, which cannot cross the binding
 * because only this package is bound.</p>
 */
public interface IIntercomUserJwtProvider {
    /**
     * Called when Intercom needs a new JWT.
     * @param callback {IIntercomUserJwtCallback} Must be completed exactly once.
     */
    void provideUserJwt(IIntercomUserJwtCallback callback);
}
