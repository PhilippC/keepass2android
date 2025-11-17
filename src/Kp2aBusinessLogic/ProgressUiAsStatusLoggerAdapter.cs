// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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
        _progressUi?.Hide();
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
        return !Java.Lang.Thread.Interrupted();
    }

    public void UpdateMessage(UiStringKey stringKey)
    {
        if (_app != null)
            UpdateMessage(_app.GetResourceString(stringKey));
    }

    public string LastMessage { get { return _lastMessage; } }
    public string LastSubMessage { get { return _lastSubMessage; } }
}