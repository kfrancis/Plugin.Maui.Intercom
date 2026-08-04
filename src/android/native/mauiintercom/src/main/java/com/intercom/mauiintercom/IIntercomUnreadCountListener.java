package com.intercom.mauiintercom;

/**
 * Notified when the unread conversation count changes.
 */
public interface IIntercomUnreadCountListener {
    /**
     * Called with the new unread conversation count.
     * @param count {int} The number of unread conversations.
     */
    void onCountUpdate(int count);
}
