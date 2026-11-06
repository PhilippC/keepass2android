package keepass2android.miuix.screens

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.ButtonDefaults
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TextField
import top.yukonga.miuix.kmp.basic.TextButton
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.Hide
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.icon.extended.Show
import top.yukonga.miuix.kmp.preference.SwitchPreference
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.DatabaseState
import keepass2android.miuix.data.RecentDatabase
import keepass2android.miuix.kdbx.KdbxEngine

@Composable
fun UnlockScreen(
    database: RecentDatabase,
    keyFileName: String?,
    keyFilePath: String? = null,
    onPickFile: ((onResult: (name: String, path: String, size: Long) -> Unit) -> Unit)? = null,
    onBack: () -> Unit,
    onUnlocked: () -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val scope = rememberCoroutineScope()
    var password by remember { mutableStateOf("") }
    var showPassword by remember { mutableStateOf(false) }
    var biometric by remember { mutableStateOf(true) }
    var rememberKeyFile by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf(false) }
    var keyName by remember(database) { mutableStateOf(keyFileName) }
    var keyPath by remember(database) { mutableStateOf(keyFilePath) }
    var unlocking by remember { mutableStateOf(false) }
    var unlockError by remember { mutableStateOf<String?>(null) }

    fun unlock() {
        if (unlocking) return
        if (password.isEmpty() && keyPath == null) {
            error = true
            return
        }
        val realPath = database.realPath
        if (realPath == null) {
            onUnlocked()
            return
        }
        scope.launch {
            unlocking = true
            unlockError = null
            try {
                val content = KdbxEngine.unlock(realPath, password, keyPath)
                DatabaseState.loadReal(content.groups, content.entries)
                onUnlocked()
            } catch (e: Exception) {
                unlockError = e.message ?: "解锁失败"
            }
            unlocking = false
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = database.name,
                navigationIcon = {
                    IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                        Icon(MiuixIcons.Back, "返回")
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(PaddingValues(start = 16.dp, end = 16.dp, top = padding.calculateTopPadding() + 8.dp)),
        ) {
            Text(database.path, color = colors.onSurfaceVariantSummary, modifier = Modifier.padding(bottom = 4.dp))
            if (keyName != null) {
                Text(
                    "密钥文件：$keyName",
                    color = colors.onSurfaceVariantSummary,
                    modifier = Modifier.padding(bottom = 16.dp),
                )
            } else {
                Spacer(Modifier.height(8.dp))
                TextButton(
                    text = "选择密钥文件（可选）",
                    onClick = { onPickFile?.invoke { name, path, _ ->
                        keyName = name
                        keyPath = path
                    } },
                    colors = top.yukonga.miuix.kmp.basic.ButtonDefaults.textButtonColors(
                        color = colors.secondaryContainer,
                        textColor = colors.primary,
                    ),
                )
                Spacer(Modifier.height(4.dp))
            }

            Card {
                TextField(
                    value = password,
                    onValueChange = {
                        password = it
                        error = false
                    },
                    label = "主密码",
                    leadingIcon = { Icon(MiuixIcons.Lock, null, tint = colors.primary) },
                    trailingIcon = {
                        IconButton(onClick = { showPassword = !showPassword }) {
                            Icon(
                                if (showPassword) MiuixIcons.Hide else MiuixIcons.Show,
                                if (showPassword) "隐藏" else "显示",
                            )
                        }
                    },
                    singleLine = true,
                )
            }

            if (error) {
                Text(
                    if (keyPath == null) "请输入主密码。" else "请输入主密码或选择密钥文件。",
                    color = colors.error,
                    modifier = Modifier.padding(start = 16.dp, top = 10.dp),
                )
            }
            unlockError?.let {
                Text(
                    it,
                    color = colors.error,
                    modifier = Modifier.padding(start = 16.dp, top = 10.dp),
                )
            }

            Spacer(Modifier.height(16.dp))

            Card {
                SwitchPreference(
                    checked = biometric,
                    onCheckedChange = { biometric = it },
                    title = "生物识别解锁",
                    summary = "使用指纹或面容快速解锁",
                )
                SwitchPreference(
                    checked = rememberKeyFile,
                    onCheckedChange = { rememberKeyFile = it },
                    title = "记住密钥文件",
                    summary = "下次打开时不再询问",
                )
            }

            Spacer(Modifier.height(24.dp))

            Button(
                onClick = ::unlock,
                enabled = !unlocking,
                modifier = Modifier.fillMaxWidth(),
                colors = ButtonDefaults.buttonColorsPrimary(),
            ) {
                Text(if (unlocking) "解锁中…" else "解锁")
            }

            Spacer(Modifier.height(padding.calculateBottomPadding() + 32.dp))
        }
    }
}
