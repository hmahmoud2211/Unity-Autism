package com.numu.unity;

/**
 * NUMU Unity-as-a-Library bridge (Android). Compiled into the exported unityLibrary module.
 * The React Native native module registers a listener; Unity calls {@link #sendToHost(String)}
 * with JSON envelopes such as {"type":"assessmentResult","payload":"{...}"}.
 */
public final class NumuUnityBridge {

    public interface Listener {
        void onUnityMessage(String message);
    }

    private static Listener listener;

    private NumuUnityBridge() {
    }

    public static synchronized void setListener(Listener l) {
        listener = l;
    }

    /** Called from C# (NumuBridge.SendToHost). */
    public static void sendToHost(String message) {
        Listener l;
        synchronized (NumuUnityBridge.class) {
            l = listener;
        }
        if (l != null) {
            l.onUnityMessage(message);
        } else {
            android.util.Log.w("NUMU", "No host listener registered; dropping message: " + message);
        }
    }
}
