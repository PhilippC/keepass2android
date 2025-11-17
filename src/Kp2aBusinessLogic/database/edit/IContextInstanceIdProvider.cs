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

namespace keepass2android;

// A context instance can be the instance of an Activity. Even if the activity is recreated (due to a configuration change, for example), the instance id must remain the same
// but it must be different for other activities/services or if the activity is finished and then starts again.
// We want to be able to perform actions on a context instance, even though that instance might not live at the time when we want to perform the action.
// In that case, we want to be able to register the action such that it is performed when the activity is recreated.
public interface IContextInstanceIdProvider
{

  int ContextInstanceId { get; }


}