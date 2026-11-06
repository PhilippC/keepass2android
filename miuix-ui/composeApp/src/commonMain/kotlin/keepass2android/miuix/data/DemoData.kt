// KeePass2Android miuix UI 原型 — 演示数据模型
package keepass2android.miuix.data

import androidx.compose.ui.graphics.vector.ImageVector
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.BankCards
import top.yukonga.miuix.kmp.icon.extended.Contacts
import top.yukonga.miuix.kmp.icon.extended.Email
import top.yukonga.miuix.kmp.icon.extended.Link
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.icon.extended.Notes
import top.yukonga.miuix.kmp.icon.extended.WorldClock

data class EntryField(
    val title: String,
    val value: String,
    val protected: Boolean = false,
)

data class KpEntry(
    val id: Int,
    val groupId: Int,
    val title: String,
    val userName: String,
    val icon: ImageVector,
    val isFavorite: Boolean = false,
    val totp: String? = null,
    val notes: String? = null,
    val fields: List<EntryField> = emptyList(),
)

data class KpGroup(
    val id: Int,
    val parentId: Int,
    val title: String,
)

data class RecentDatabase(
    val name: String,
    val path: String,
    val realPath: String? = null,
)

object DemoData {

    val recentDatabases = listOf(
        RecentDatabase("Keys.kdbx", "/storage/emulated/0/Documents/Keys.kdbx"),
        RecentDatabase("work.kdbx", "https://drive.example.com/work.kdbx"),
        RecentDatabase("家庭密码.kdbx", "/storage/emulated/0/keepass/家庭密码.kdbx"),
    )

    val groups = listOf(
        KpGroup(0, -1, "Keys.kdbx"),
        KpGroup(1, 0, "邮箱"),
        KpGroup(2, 0, "金融"),
        KpGroup(3, 0, "开发"),
        KpGroup(4, 0, "社交网络"),
        KpGroup(5, 2, "银行卡"),
    )

    val entries = listOf(
        KpEntry(
            id = 1, groupId = 1, title = "Gmail", userName = "demo@gmail.com",
            icon = MiuixIcons.Email, isFavorite = true, totp = "492 831",
            notes = "备用邮箱：demo.backup@outlook.com\n两步验证已开启。",
            fields = listOf(
                EntryField("用户名", "demo@gmail.com"),
                EntryField("密码", "P@ssw0rd-Demo-2026", protected = true),
                EntryField("网址", "https://mail.google.com"),
            ),
        ),
        KpEntry(
            id = 2, groupId = 1, title = "Outlook", userName = "demo@outlook.com",
            icon = MiuixIcons.Email,
            fields = listOf(
                EntryField("用户名", "demo@outlook.com"),
                EntryField("密码", "0utl00k!Secure", protected = true),
                EntryField("网址", "https://outlook.live.com"),
            ),
        ),
        KpEntry(
            id = 3, groupId = 2, title = "招商银行", userName = "6222 0200 0012 3456",
            icon = MiuixIcons.BankCards, isFavorite = true,
            notes = "网银登录后需要短信二次验证。",
            fields = listOf(
                EntryField("卡号", "6222 0200 0012 3456", protected = true),
                EntryField("查询密码", "123456", protected = true),
                EntryField("网址", "https://www.cmbchina.com"),
            ),
        ),
        KpEntry(
            id = 4, groupId = 2, title = "支付宝", userName = "demo@gmail.com",
            icon = MiuixIcons.WorldClock,
            fields = listOf(
                EntryField("账户", "demo@gmail.com"),
                EntryField("支付密码", "Alipay@2026", protected = true),
            ),
        ),
        KpEntry(
            id = 5, groupId = 3, title = "GitHub", userName = "kp2a-demo",
            icon = MiuixIcons.Link, isFavorite = true, totp = "108 552",
            fields = listOf(
                EntryField("用户名", "kp2a-demo"),
                EntryField("密码", "gh-Demo#7733", protected = true),
                EntryField("网址", "https://github.com"),
            ),
        ),
        KpEntry(
            id = 6, groupId = 3, title = "公司 VPN", userName = "zhang.san",
            icon = MiuixIcons.Lock,
            fields = listOf(
                EntryField("用户名", "zhang.san"),
                EntryField("密码", "Vpn!Demo-9900", protected = true),
                EntryField("服务器", "vpn.example.com:1194"),
            ),
        ),
        KpEntry(
            id = 7, groupId = 4, title = "微博", userName = "@demo_user",
            icon = MiuixIcons.Contacts,
            fields = listOf(
                EntryField("账号", "@demo_user"),
                EntryField("密码", "We1bo@Demo", protected = true),
                EntryField("网址", "https://weibo.com"),
            ),
        ),
        KpEntry(
            id = 8, groupId = 5, title = "工资卡", userName = "6222 8480 0099 1122",
            icon = MiuixIcons.BankCards,
            fields = listOf(
                EntryField("卡号", "6222 8480 0099 1122", protected = true),
                EntryField("取款密码", "882233", protected = true),
            ),
        ),
        KpEntry(
            id = 9, groupId = 0, title = "家庭 WiFi", userName = "Home-5G",
            icon = MiuixIcons.Notes,
            fields = listOf(
                EntryField("网络名称", "Home-5G"),
                EntryField("密码", "WiFi-Demo-5678", protected = true),
            ),
        ),
    )

    fun subgroupsOf(groupId: Int): List<KpGroup> = groups.filter { it.parentId == groupId }

    fun entriesOf(groupId: Int): List<KpEntry> = entries.filter { it.groupId == groupId }

    fun findEntry(id: Int): KpEntry? = entries.firstOrNull { it.id == id }

    fun findGroup(id: Int): KpGroup? = groups.firstOrNull { it.id == id }

    fun entryCountRecursive(groupId: Int): Int {
        val direct = entriesOf(groupId).size
        return direct + subgroupsOf(groupId).sumOf { entryCountRecursive(it.id) }
    }
}

object PasswordGenerator {
    private const val LOWER = "abcdefghijklmnopqrstuvwxyz"
    private const val UPPER = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    private const val DIGITS = "0123456789"
    private const val SYMBOLS = "!@#\$%^&*()-_=+[]{};:,.<>?"

    fun generate(length: Int, useUpper: Boolean, useDigits: Boolean, useSymbols: Boolean): String {
        val pools = buildList {
            add(LOWER)
            if (useUpper) add(UPPER)
            if (useDigits) add(DIGITS)
            if (useSymbols) add(SYMBOLS)
        }
        if (pools.isEmpty()) return ""
        val alphabet = pools.joinToString("")
        return buildString {
            repeat(length) { append(alphabet.random()) }
        }
    }
}
