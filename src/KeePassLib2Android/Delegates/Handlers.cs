/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2017 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;

namespace KeePassLib.Delegates
{
  /// <summary>
  /// Function definition of a method that performs an action on a group.
  /// When traversing the internal tree, this function will be invoked
  /// for all visited groups.
  /// </summary>
  /// <param name="pg">Currently visited group.</param>
  /// <returns>You must return <c>true</c> if you want to continue the
  /// traversal. If you want to immediately stop the whole traversal,
  /// return <c>false</c>.</returns>
  public delegate bool GroupHandler(PwGroup pg);

  /// <summary>
  /// Function definition of a method that performs an action on an entry.
  /// When traversing the internal tree, this function will be invoked
  /// for all visited entries.
  /// </summary>
  /// <param name="pe">Currently visited entry.</param>
  /// <returns>You must return <c>true</c> if you want to continue the
  /// traversal. If you want to immediately stop the whole traversal,
  /// return <c>false</c>.</returns>
  public delegate bool EntryHandler(PwEntry pe);

  public delegate void VoidDelegate();

  public delegate string StrPwEntryDelegate(string str, PwEntry pe);

  public delegate TResult GFunc<TResult>();
  public delegate TResult GFunc<T, TResult>(T o);
  public delegate TResult GFunc<T1, T2, TResult>(T1 o1, T2 o2);
  public delegate TResult GFunc<T1, T2, T3, TResult>(T1 o1, T2 o2, T3 o3);
  public delegate TResult GFunc<T1, T2, T3, T4, TResult>(T1 o1, T2 o2, T3 o3, T4 o4);
  public delegate TResult GFunc<T1, T2, T3, T4, T5, TResult>(T1 o1, T2 o2, T3 o3, T4 o4, T5 o5);
  public delegate TResult GFunc<T1, T2, T3, T4, T5, T6, TResult>(T1 o1, T2 o2, T3 o3, T4 o4, T5 o5, T6 o6);
}
