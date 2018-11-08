package keepass2android.softkeyboard;

import keepass2android.kbbridge.StringForTyping;

public interface IKeyboardService
{
    void commitStringForTyping(StringForTyping stringForTyping);
    void onNewData();
}
