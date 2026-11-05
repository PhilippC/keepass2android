package keepass2android.miuix.screens

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Slider
import top.yukonga.miuix.kmp.basic.SmallTitle
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.Copy
import top.yukonga.miuix.kmp.icon.extended.Refresh
import top.yukonga.miuix.kmp.preference.SwitchPreference
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.PasswordGenerator

@Composable
fun GeneratePasswordScreen(onBack: () -> Unit) {
    val colors = MiuixTheme.colorScheme
    val scrollBehavior = MiuixScrollBehavior()
    val clipboard = LocalClipboardManager.current

    var length by rememberSaveable { mutableFloatStateOf(18f) }
    var useUpper by rememberSaveable { mutableStateOf(true) }
    var useDigits by rememberSaveable { mutableStateOf(true) }
    var useSymbols by rememberSaveable { mutableStateOf(true) }
    var refreshSeed by rememberSaveable { mutableIntStateOf(0) }

    val password = remember(length.toInt(), useUpper, useDigits, useSymbols, refreshSeed) {
        PasswordGenerator.generate(length.toInt(), useUpper, useDigits, useSymbols)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = "密码生成器",
                scrollBehavior = scrollBehavior,
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
            Card(modifier = Modifier.fillMaxWidth()) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.padding(vertical = 8.dp),
                ) {
                    Text(
                        password,
                        fontSize = 18.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = colors.primary,
                        modifier = Modifier.weight(1f),
                    )
                    IconButton(onClick = { refreshSeed++ }) {
                        Icon(MiuixIcons.Refresh, "重新生成")
                    }
                    IconButton(onClick = { clipboard.setText(AnnotatedString(password)) }) {
                        Icon(MiuixIcons.Copy, "复制")
                    }
                }
            }

            SmallTitle("长度：${length.toInt()} 位")
            Card(modifier = Modifier.fillMaxWidth()) {
                Slider(
                    value = length,
                    onValueChange = { length = it },
                    valueRange = 4f..64f,
                    steps = 59,
                    modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
                )
            }

            Spacer(Modifier.height(12.dp))

            SmallTitle("字符类型")
            Card(modifier = Modifier.fillMaxWidth()) {
                SwitchPreference(
                    checked = useUpper,
                    onCheckedChange = { useUpper = it },
                    title = "大写字母",
                    summary = "A-Z",
                )
                SwitchPreference(
                    checked = useDigits,
                    onCheckedChange = { useDigits = it },
                    title = "数字",
                    summary = "0-9",
                )
                SwitchPreference(
                    checked = useSymbols,
                    onCheckedChange = { useSymbols = it },
                    title = "符号",
                    summary = "!@#$%^&* 等",
                )
            }

            Spacer(Modifier.height(padding.calculateBottomPadding() + 32.dp))
        }
    }
}
