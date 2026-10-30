package com.intercom.mauiintercom;

/**
 * Answers one request from an IIntercomUserJwtProvider.
 *
 * <p>A single nullable method rather than Intercom's onSuccess/onFailure pair, matching the
 * iOS completion block: the C# side has one answer to give, a JWT or none.</p>
 */
public interface IIntercomUserJwtCallback {
    /**
     * Completes the request.
     * @param jwt {String} The new JWT, or null (or empty) when none can be supplied.
     */
    void onComplete(String jwt);
}
