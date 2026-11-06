package keepass2android.miuix.screens

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.launch
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.ButtonDefaults
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Checkbox
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.SmallTitle
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TextButton
import top.yukonga.miuix.kmp.basic.TextField
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.RecentDatabase
import keepass2android.miuix.kdbx.KdbxEngine

@Composable
fun CreateDatabaseScreen(
    defaultPath: String,
    onPickFile: ((onResult: (name: String, path: String, size: Long) -> Unit) -> Unit)? = null,
    onCreateFile: (suggestedName: String, onResult: (name: String, path: String) -> Unit) -> Unit = { _, _ -> },
    onBack: () -> Unit,
    onCreated: (RecentDatabase) -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val scope = rememberCoroutineScope()
    var dbPath by remember { mutableStateOf(defaultPath) }
    var dbName by remember { mutableStateOf<String?>(null) }
    var password by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var showPassword by remember { mutableStateOf(false) }
    var useKeyFile by remember { mutableStateOf(false) }
    var keyName by remember { mutableStateOf<String?>(null) }
    var keyPath by remember { mutableStateOf<String?>(null) }
    var creating by remember { mutableStateOf(false) }
    var errorMsg by remember { mutableStateOf<String?>(null) }

    fun submit() {
        if (creating) return
        when {
            dbPath.isBlank() -> errorMsg = "请选择数据库存储位置。"
            password.isEmpty() -> errorMsg = "请输入主密码。"
            password != confirmPassword -> errorMsg = "两次输入的密码不一致。"
            useKeyFile && keyPath == null -> errorMsg = "请选择密钥文件。"
            else -> {
                errorMsg = null
                creating = true
                scope.launch {
                    try {
                        KdbxEngine.create(dbPath, password, if (useKeyFile) keyPath else null)
                        creating = false
                        onCreated(
                            RecentDatabase(
                                name = dbName ?: dbPath.substringAfterLast('/').ifEmpty { "keepass.kdbx" },
                                path = dbPath,
                                realPath = dbPath,
                            ),
                        )
                    } catch (e: Exception) {
                        creating = false
                        errorMsg = e.message ?: "创建数据库失败"
                    }
                }
            }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = "创建数据库",
                scrollBehavior = MiuixScrollBehavior(),
                navigationIcon = {
                    top.yukonga.miuix.kmp.basic.IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                        top.yukonga.miuix.kmp.basic.Icon(MiuixIcons.Back, "返回")
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(top = padding.calculateTopPadding()),
        ) {
            SmallTitle("数据库位置")
            Card(modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp)) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("选择数据库存储的位置：", fontSize = 13.sp, color = colors.onSurfaceVariantSummary)
                    Spacer(Modifier.height(8.dp))
                    Text(dbPath.ifBlank { "未选择" }, fontSize = 13.sp, color = colors.onSurface)
                    Spacer(Modifier.height(10.dp))
                    TextButton(text = "更改位置", onClick = {
                        onCreateFile("keepass.kdbx") { name, path ->
                            dbName = name
                            dbPath = path
                        }
                    })
                }
            }

            SmallTitle("主密码")
            Card(modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp)) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("请选择一个主密码来保护您的数据库：", fontSize = 13.sp, color = colors.onSurfaceVariantSummary)
                    Spacer(Modifier.height(10.dp))
                    TextField(
                        value = password,
                        onValueChange = {
                            password = it
                            errorMsg = null
                        },
                        label = "密码",
                        singleLine = true,
                        visualTransformation = if (showPassword) {
                            androidx.compose.ui.text.input.VisualTransformation.None
                        } else {
                            androidx.compose.ui.text.input.PasswordVisualTransformation()
                        },
                    )
                    Spacer(Modifier.height(10.dp))
                    TextField(
                        value = confirmPassword,
                        onValueChange = {
                            confirmPassword = it
                            errorMsg = null
                        },
                        label = "确认密码",
                        singleLine = true,
                        visualTransformation = if (showPassword) {
                            androidx.compose.ui.text.input.VisualTransformation.None
                        } else {
                            androidx.compose.ui.text.input.PasswordVisualTransformation()
                        },
                    )
                    Spacer(Modifier.height(8.dp))
                    TextButton(
                        text = if (showPassword) "隐藏密码" else "显示密码",
                        onClick = { showPassword = !showPassword },
                    )
                }
            }

            SmallTitle("密钥文件")
            Card(modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp)) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("勾选以在使用主密码之外额外使用一个密钥文件：", fontSize = 13.sp, color = colors.onSurfaceVariantSummary)
                    Spacer(Modifier.height(8.dp))
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Checkbox(
                            state = androidx.compose.ui.state.ToggleableState(useKeyFile),
                            onClick = { useKeyFile = !useKeyFile },
                        )
                        Spacer(Modifier.width(8.dp))
                        Text("使用密钥文件", color = colors.onSurface)
                    }
                    if (useKeyFile) {
                        Spacer(Modifier.height(8.dp))
                        if (keyName != null) {
                            Text("已选择：$keyName", fontSize = 13.sp, color = colors.onSurface)
                        }
                        Spacer(Modifier.height(6.dp))
                        TextButton(text = "选择密钥文件", onClick = {
                            onPickFile?.invoke { name, path, _ ->
                                keyName = name
                                keyPath = path
                            }
                        })
                    }
                }
            }

            errorMsg?.let {
                Text(
                    it,
                    color = colors.error,
                    fontSize = 13.sp,
                    modifier = Modifier.padding(horizontal = 20.dp, vertical = 8.dp),
                )
            }

            Spacer(Modifier.height(12.dp))
            Button(
                onClick = ::submit,
                enabled = !creating,
                colors = ButtonDefaults.buttonColorsPrimary(),
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp)
                    .height(50.dp),
            ) {
                Text(if (creating) "创建中…" else "创建数据库")
            }
            Spacer(Modifier.height(32.dp))
        }
    }
}
