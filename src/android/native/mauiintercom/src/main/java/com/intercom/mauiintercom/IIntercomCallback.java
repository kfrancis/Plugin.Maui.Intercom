package com.intercom.mauiintercom;

public interface IIntercomCallback {
    void onSuccess();
    void onFailure(String error);
}
