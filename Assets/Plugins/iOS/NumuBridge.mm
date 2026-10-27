// NUMU Unity-as-a-Library bridge (iOS).
// The React Native host registers a handler once; Unity calls NumuSendToHost with JSON envelopes
// ({"type": "...", "payload": "..."}), e.g. assessment results, scenario finished, exit.

#import <Foundation/Foundation.h>

typedef void (*NumuHostMessageHandler)(const char *message);

static NumuHostMessageHandler sNumuHandler = NULL;

extern "C" {

/// Called by the host app (e.g. from the RN native module) to receive Unity messages.
void NumuRegisterHostHandler(NumuHostMessageHandler handler)
{
    sNumuHandler = handler;
}

/// Called from C# (NumuBridge.SendToHost).
void NumuSendToHost(const char *message)
{
    if (message == NULL) return;
    if (sNumuHandler != NULL)
    {
        sNumuHandler(message);
    }
    else
    {
        NSLog(@"[NUMU] No host handler registered; dropping message: %s", message);
    }
}

}
