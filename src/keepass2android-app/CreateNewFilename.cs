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

using System;
using Android.App;
using KeePassLib.Serialization;

namespace keepass2android
{
    class CreateNewFilename : OperationWithFinishHandler
    {
        private readonly string _filename;

        public CreateNewFilename(IKp2aApp app, OnOperationFinishedHandler operationFinishedHandler, string filename)
            : base(app, operationFinishedHandler)
        {
            _filename = filename;
        }

        public override void Run()
        {
            try
            {
                int lastIndexOfSlash = _filename.LastIndexOf("/", StringComparison.Ordinal);
                string parent = _filename.Substring(0, lastIndexOfSlash);
                string newFilename = _filename.Substring(lastIndexOfSlash + 1);

                string resultingFilename = App.Kp2a.GetFileStorage(new IOConnectionInfo { Path = parent }).CreateFilePath(parent, newFilename);

                Finish(true, resultingFilename);
            }
            catch (Exception e)
            {
                Finish(false, Util.GetErrorMessage(e));
            }

        }
    }
}