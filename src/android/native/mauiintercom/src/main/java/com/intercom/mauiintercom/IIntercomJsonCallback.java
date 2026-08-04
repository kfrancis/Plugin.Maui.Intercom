package com.intercom.mauiintercom;

/**
 * Result of an Intercom operation that returns data.
 *
 * <p>The payload is JSON rather than a bound model type on purpose. The Help Center models
 * are Kotlin data classes whose generated members (component1, copy, $stable, the
 * kotlinx.serialization plumbing) would all cross the binding for read-only data. Serialising
 * them here keeps the bound surface to this one interface and puts the shape in a single
 * place on each side.</p>
 */
public interface IIntercomJsonCallback {
    /**
     * Called when the operation succeeded.
     * @param json {String} The result, serialised as JSON.
     */
    void onComplete(String json);

    /**
     * Called when the operation failed.
     * @param errorCode {int} The HTTP-ish error code the SDK reported, or -1 when it reported none.
     * @param message {String} The error message.
     */
    void onFailure(int errorCode, String message);
}
