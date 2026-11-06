package keepass2android.miuix.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.ButtonDefaults
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.SmallTitle
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.preference.ArrowPreference
import top.yukonga.miuix.kmp.preference.SwitchPreference
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.ThemeMode

@Composable
fun SettingsScreen(
    themeMode: ThemeMode,
    onThemeModeChange: (ThemeMode) -> Unit,
    databasePath: String,
) {
    val scrollBehavior = MiuixScrollBehavior()
    var clipboardAutoClear by remember { mutableStateOf(true) }
    var hideProtected by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = "设置",
                scrollBehavior = scrollBehavior,
            )
        },
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(
                start = 16.dp,
                end = 16.dp,
                top = padding.calculateTopPadding() + 8.dp,
                bottom = padding.calculateBottomPadding() + 32.dp,
            ),
        ) {
            item { SmallTitle("外观") }
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 12.dp, vertical = 12.dp),
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                    ) {
                        ThemeMode.entries.forEach { mode ->
                            Button(
                                onClick = { onThemeModeChange(mode) },
                                modifier = Modifier.weight(1f),
                                colors = if (themeMode == mode) {
                                    ButtonDefaults.buttonColorsPrimary()
                                } else {
                                    ButtonDefaults.buttonColors()
                                },
                            ) {
                                Text(mode.label)
                            }
                        }
                    }
                }
            }

            item { SmallTitle("安全") }
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    SwitchPreference(
                        checked = clipboardAutoClear,
                        onCheckedChange = { clipboardAutoClear = it },
                        title = "剪贴板自动清除",
                        summary = "复制 30 秒后自动清空剪贴板",
                    )
                    SwitchPreference(
                        checked = hideProtected,
                        onCheckedChange = { hideProtected = it },
                        title = "始终隐藏受保护字段",
                        summary = "查看时需要手动点击显示",
                    )
                    ArrowPreference(
                        title = "自动锁定时间",
                        summary = "应用切入后台 5 分钟后锁定",
                        onClick = { /* 原型未实现 */ },
                    )
                }
            }

            item { SmallTitle("数据库") }
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    ArrowPreference(
                        title = "数据库位置",
                        summary = databasePath,
                        onClick = { /* 原型未实现 */ },
                    )
                    ArrowPreference(
                        title = "导出数据库",
                        summary = "导出为 .kdbx 或 CSV",
                        onClick = { /* 原型未实现 */ },
                    )
                }
            }

            item { SmallTitle("关于") }
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    ArrowPreference(
                        title = "版本",
                        summary = "1.0.0",
                        onClick = { /* 原型未实现 */ },
                    )
                    ArrowPreference(
                        title = "开源许可",
                        summary = "GPLv3 · 基于 miuix 0.9.4",
                        onClick = { /* 原型未实现 */ },
                    )
                }
            }
        }
    }
}
