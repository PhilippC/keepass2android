using System;
using System.Collections.Generic;
using System.Diagnostics;
using Android.Webkit;
using KeePass.Util.Spr;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android
{
    public sealed class AutoExecItem
    {
        private PwEntry m_pe;
        public PwEntry Entry
        {
            get { return m_pe; }
        }

        private PwDatabase m_pdContext;
        public PwDatabase Database
        {
            get { return m_pdContext; }
        }

        public bool Enabled = true;
        public bool Visible = true;

        public long Priority = 0;

        public AutoExecItem(PwEntry pe, PwDatabase pdContext)
        {
            if (pe == null) throw new ArgumentNullException("pe");

            m_pe = pe;
            m_pdContext = pdContext;
        }
    }

    public sealed class KeeAutoExecExt
    {


        private static int PrioritySort(AutoExecItem x, AutoExecItem y)
        {
            if (x == null) { Debug.Assert(false); return ((y == null) ? 0 : -1); }
            if (y == null) { Debug.Assert(false); return 1; }

            return x.Priority.CompareTo(y.Priority);
        }

        private static void AddAutoExecEntries(List<PwEntry> l, PwGroup pg)
        {
            if (pg.Name.Equals("AutoOpen", StrUtil.CaseIgnoreCmp))
                l.AddRange(pg.GetEntries(true));
            else
            {
                foreach (PwGroup pgSub in pg.Groups)
                    AddAutoExecEntries(l, pgSub);
            }
        }

        private static List<AutoExecItem> GetAutoExecItems(PwDatabase pd)
        {
            List<AutoExecItem> l = new List<AutoExecItem>();
            if (pd == null) { Debug.Assert(false); return l; }
            if (!pd.IsOpen) return l;

            PwGroup pgRoot = pd.RootGroup;
            if (pgRoot == null) { Debug.Assert(false); return l; }

            List<PwEntry> lAutoEntries = new List<PwEntry>();
            AddAutoExecEntries(lAutoEntries, pgRoot);

            long lPriStd = 0;
            foreach (PwEntry pe in lAutoEntries)
            {
                string str = pe.Strings.ReadSafe(PwDefs.UrlField);
                if (str.Length == 0) continue;

                AutoExecItem a = new AutoExecItem(pe, pd);
                l.Add(a);

                SprContext ctx = new SprContext(pe, pd, SprCompileFlags.All);

                if (pe.Expires && (pe.ExpiryTime <= DateTime.UtcNow))
                    a.Enabled = false;

                bool? ob = GetBoolEx(pe, "Enabled", ctx);
                if (ob.HasValue) a.Enabled = ob.Value;

                ob = GetBoolEx(pe, "Visible", ctx);
                if (ob.HasValue) a.Visible = ob.Value;

                long lItemPri = lPriStd;
                if (GetString(pe, "Priority", ctx, true, out str))
                    long.TryParse(str, out lItemPri);
                a.Priority = lItemPri;

                ++lPriStd;
            }

            l.Sort(KeeAutoExecExt.PrioritySort);
            return l;
        }

        private void OnFileOpen(PwDatabase db)
        {
            List<AutoExecItem> l = GetAutoExecItems(db);
            foreach (AutoExecItem a in l)
            {
                if (!a.Enabled) continue;

                try { AutoOpenEntryPriv(a, false); }
                catch (Exception ex)
                {
                    MessageService.ShowWarning(ex);
                }
            }
        }

        private void AutoOpenEntryPriv(AutoExecItem a, bool bManual)
        {
            PwEntry pe = a.Entry;
            PwDatabase pdContext = a.Database;

            SprContext ctxNoEsc = new SprContext(pe, pdContext, SprCompileFlags.All);
            SprContext ctxEsc = new SprContext(pe, pdContext, SprCompileFlags.All,
                false, true);

            string strDb;
            if (!GetString(pe, PwDefs.UrlField, ctxEsc, true, out strDb)) return;

            IOConnectionInfo ioc = IOConnectionInfo.FromPath(strDb);
            //TODO
            /*if (ioc.IsLocalFile() && !UrlUtil.IsAbsolutePath(strDb))
                ioc = IOConnectionInfo.FromPath(UrlUtil.MakeAbsolutePath(
                    WinUtil.GetExecutable(), strDb));*/
            if (ioc.Path.Length == 0) return;

            string strIocUserName;
            if (GetString(pe, "IocUserName", ctxNoEsc, true, out strIocUserName))
                ioc.UserName = strIocUserName;

            string strIocPassword;
            if (GetString(pe, "IocPassword", ctxNoEsc, true, out strIocPassword))
                ioc.Password = strIocPassword;

            if ((strIocUserName.Length != 0) && (strIocPassword.Length != 0))
                ioc.IsComplete = true;

            string str;
            if (GetString(pe, "IocTimeout", ctxNoEsc, true, out str))
            {
                long l;
                if (long.TryParse(str, out l))
                    ioc.Properties.SetLong(IocKnownProperties.Timeout, l);
            }

            bool? ob = GetBoolEx(pe, "IocPreAuth", ctxNoEsc);
            if (ob.HasValue)
                ioc.Properties.SetBool(IocKnownProperties.PreAuth, ob.Value);

            if (GetString(pe, "IocUserAgent", ctxNoEsc, true, out str))
                ioc.Properties.Set(IocKnownProperties.UserAgent, str);

            ob = GetBoolEx(pe, "IocExpect100Continue", ctxNoEsc);
            if (ob.HasValue)
                ioc.Properties.SetBool(IocKnownProperties.Expect100Continue, ob.Value);

            ob = GetBoolEx(pe, "IocPassive", ctxNoEsc);
            if (ob.HasValue)
                ioc.Properties.SetBool(IocKnownProperties.Passive, ob.Value);

            ob = GetBoolEx(pe, "SkipIfNotExists", ctxNoEsc);
            if (!ob.HasValue) // Backw. compat.
                ob = GetBoolEx(pe, "Skip if not exists", ctxNoEsc);
            if (ob.HasValue && ob.Value)
            {
                if (!IOConnection.FileExists(ioc)) return;
            }

            CompositeKey ck = new CompositeKey();

            if (GetString(pe, PwDefs.PasswordField, ctxNoEsc, false, out str))
                ck.AddUserKey(new KcpPassword(str));

            if (GetString(pe, PwDefs.UserNameField, ctxNoEsc, false, out str))
            {
                string strAbs = str;
                IOConnectionInfo iocKey = IOConnectionInfo.FromPath(strAbs);
                if (iocKey.IsLocalFile() && !UrlUtil.IsAbsolutePath(strAbs))
                {
                    //TODO
                    /*      strAbs = UrlUtil.MakeAbsolutePath(WinUtil.GetExecutable(), strAbs);*/
                }


                ob = GetBoolEx(pe, "SkipIfKeyFileNotExists", ctxNoEsc);
                if (ob.HasValue && ob.Value)
                {
                    IOConnectionInfo iocKeyAbs = IOConnectionInfo.FromPath(strAbs);
                    if (!IOConnection.FileExists(iocKeyAbs)) return;
                }

                try { ck.AddUserKey(new KcpKeyFile(strAbs)); }
                catch (InvalidOperationException)
                {
                    //TODO
                    throw new Exception("TODO");
                    //throw new Exception(strAbs + MessageService.NewParagraph + KPRes.KeyFileError);
                }
                catch (Exception) { throw; }
            }
            else // Try getting key file from attachments
            {
                ProtectedBinary pBin = pe.Binaries.Get("KeyFile.bin");
                if (pBin != null)
                    ck.AddUserKey(new KcpKeyFile(IOConnectionInfo.FromPath(
                        StrUtil.DataToDataUri(pBin.ReadData(), null))));
            }

            if (GetString(pe, "KeyProvider", ctxNoEsc, true, out str))
            {
                /*TODO KeyProvider kp = m_host.KeyProviderPool.Get(str);
                if (kp == null)
                    throw new Exception(@"Unknown key provider: '" + str + @"'!");

                KeyProviderQueryContext ctxKP = new KeyProviderQueryContext(
                    ioc, false, false);

                bool bPerformHash = !kp.DirectKey;
                byte[] pbProvKey = kp.GetKey(ctxKP);
                if ((pbProvKey != null) && (pbProvKey.Length != 0))
                {
                    ck.AddUserKey(new KcpCustomKey(str, pbProvKey, bPerformHash));
                    MemUtil.ZeroByteArray(pbProvKey);
                }
                else return; // Provider has shown error message*/
                throw new Exception("KeyProvider not supported");
            }

            ob = GetBoolEx(pe, "UserAccount", ctxNoEsc);
            if (ob.HasValue && ob.Value)
                ck.AddUserKey(new KcpUserAccount());

            if (ck.UserKeyCount == 0) return;

            GetString(pe, "Focus", ctxNoEsc, true, out str);
            bool bRestoreFocus = str.Equals("Restore", StrUtil.CaseIgnoreCmp);
            /*TODO
             * PwDatabase pdPrev = m_host.MainWindow.ActiveDatabase;

            m_host.MainWindow.OpenDatabase(ioc, ck, true);

            if (bRestoreFocus && (pdPrev != null) && !bManual)
            {
                PwDocument docPrev = m_host.MainWindow.DocumentManager.FindDocument(
                    pdPrev);
                if (docPrev != null) m_host.MainWindow.MakeDocumentActive(docPrev);
                else { Debug.Assert(false); }
            }*/
        }

        private static bool GetString(PwEntry pe, string strName, SprContext ctx,
        bool bTrim, out string strValue)
        {
            if ((pe == null) || (strName == null))
            {
                Debug.Assert(false);
                strValue = string.Empty;
                return false;
            }

            string str = pe.Strings.ReadSafe(strName);
            if (ctx != null) str = SprEngine.Compile(str, ctx);
            if (bTrim) str = str.Trim();

            strValue = str;
            return (str.Length != 0);
        }

        private static bool? GetBoolEx(PwEntry pe, string strName, SprContext ctx)
        {
            string str;
            if (GetString(pe, strName, ctx, true, out str))
            {
                if (str.Equals("True", StrUtil.CaseIgnoreCmp))
                    return true;
                if (str.Equals("False", StrUtil.CaseIgnoreCmp))
                    return false;
            }

            return null;
        }
    }

    public class KeeAutoExec
    {

    }
}
