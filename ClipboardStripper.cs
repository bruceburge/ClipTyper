using System.Runtime.InteropServices;

namespace ClipTyper;

/// <summary>
/// The optional formatting stripper, inherited from ClipStrip (2015). When
/// enabled, clipboard content that carries formatting (RTF or HTML present)
/// is replaced with its plain text, so the next paste comes out clean.
///
/// Two deliberate differences from the original:
///  - It listens with AddClipboardFormatListener (WM_CLIPBOARDUPDATE) rather
///    than joining the legacy clipboard viewer chain, so there is no chain
///    to forward and nothing to break when another viewer misbehaves.
///  - The trigger is "RTF or HTML present", which is what "has formatting"
///    actually means. After a strip, only plain text formats remain, so the
///    clipboard update caused by our own write is a no-op and the tool can
///    never fight itself.
/// Transient clipboard contention is retried briefly and then ignored: a
/// background clipboard tool must never pop error dialogs.
/// </summary>
internal static class ClipboardStripper
{
    private static bool _writingClipboard;

    /// <summary>True while we are writing our own stripped text back.</summary>
    public static bool IsWriting => _writingClipboard;

    public static void TryStrip()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                IDataObject? data = Clipboard.GetDataObject();
                if (data == null) return;

                bool hasFormatting = data.GetDataPresent(DataFormats.Rtf)
                                  || data.GetDataPresent(DataFormats.Html);
                if (!hasFormatting) return;
                if (!data.GetDataPresent(DataFormats.UnicodeText)) return;

                if (data.GetData(DataFormats.UnicodeText) is not string text) return;
                if (text.Length == 0) return;

                _writingClipboard = true;
                try { Clipboard.SetText(text); }
                finally { _writingClipboard = false; }
                return;
            }
            catch (ExternalException)
            {
                // Another app has the clipboard open mid-copy. Retry briefly.
                Thread.Sleep(50);
            }
            catch
            {
                return;
            }
        }
    }
}
