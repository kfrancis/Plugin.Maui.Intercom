package com.intercom.mauiintercom;

/**
 * Result of an asynchronous Intercom operation.
 *
 * <p>onFailure carries the native error code as well as the message: the Messenger reports
 * nearly every failure to the user as the same generic error screen, so the code is the only
 * way for a caller to tell them apart.</p>
 */
public interface IIntercomCallback {
    /**
     * Called when the Intercom operation succeeded.
     */
    void onSuccess();

    /**
     * Called when the Intercom operation failed.
     * @param errorCode {int} IntercomError.getErrorCode(), or -1 when the failure came from
     *                  outside the SDK (for example a malformed argument).
     * @param message {String} The error message.
     */
    void onFailure(int errorCode, String message);
}
