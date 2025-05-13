using KeePassLib.Interfaces;

namespace keepass2android;

public class ProgressUiAsStatusLoggerAdapter : IKp2aStatusLogger
{
    private IProgressUi? _progressUi;
    private readonly IKp2aApp _app;

    private string _lastMessage = "";
    private string _lastSubMessage = "";
    private bool _isVisible = false;

    public ProgressUiAsStatusLoggerAdapter(IProgressUi progressUi, IKp2aApp app)
    {
        _progressUi = progressUi;
        _app = app;
    }

    public void SetNewProgressUi(IProgressUi progressUi)
    {
        _progressUi = progressUi;
        if (_isVisible)
        {
            progressUi?.Show();
            progressUi?.UpdateMessage(_lastMessage);
            progressUi?.UpdateSubMessage(_lastSubMessage);
        }
        else
        {
            progressUi?.Hide();
        }
    }

    public void StartLogging(string strOperation, bool bWriteOperationToLog)
    {
        _progressUi?.Show();
        _isVisible = true;
    }

    public void EndLogging()
    {
        _progressUi?.Hide();
        _isVisible = false;
    }

    public bool SetProgress(uint uPercent)
    {
        return true;
    }

    public bool SetText(string strNewText, LogStatusType lsType)
    {
        if (strNewText.StartsWith("KP2AKEY_"))
        {
            UiStringKey key;
            if (Enum.TryParse(strNewText.Substring("KP2AKEY_".Length), true, out key))
            {
                UpdateMessage(_app.GetResourceString(key));
                return true;
            }
        }
        UpdateMessage(strNewText);

        return true;
    }

    public void UpdateMessage(string message)
    {
        _progressUi?.UpdateMessage(message);
        _lastMessage = message;
    }

    public void UpdateSubMessage(string submessage)
    {
        _progressUi?.UpdateSubMessage(submessage);
        _lastSubMessage = submessage;
    }

    public bool ContinueWork()
    {
        return true;
    }

    public void UpdateMessage(UiStringKey stringKey)
    {
        if (_app != null)
            UpdateMessage(_app.GetResourceString(stringKey));
    }
}