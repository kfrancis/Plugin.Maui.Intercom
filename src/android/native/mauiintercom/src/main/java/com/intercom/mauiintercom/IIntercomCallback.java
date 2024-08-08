package com.intercom.mauiintercom;

/**
 * Interface for Intercom callback
 */
public interface IIntercomCallback {
    /**
     * Called when the Intercom action is successful
     */
    void onSuccess();

    /**
     * Called when the Intercom action fails
     * @param error {String} The error message
     */
    void onFailure(String error);
}
