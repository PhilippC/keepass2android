package keepass2android.miuix.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
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
import kotlinx.coroutines.delay
import top.yukonga.miuix.kmp.basic.BasicComponent
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.HorizontalDivider
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.SmallTitle
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.BankCards
import top.yukonga.miuix.kmp.icon.extended.Contacts
import top.yukonga.miuix.kmp.icon.extended.Copy
import top.yukonga.miuix.kmp.icon.extended.Hide
import top.yukonga.miuix.kmp.icon.extended.Link
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.icon.extended.Notes
import top.yukonga.miuix.kmp.icon.extended.Show
import top.yukonga.miuix.kmp.icon.extended.Timer
import top.yukonga.miuix.kmp.icon.extended.Tune
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.EntryField
import keepass2android.miuix.data.KpEntry

private fun fieldIcon(field: EntryField) = when {
    field.protected -> MiuixIcons.Lock
    field.title.contains("网址") || field.title.contains("服务器") -> MiuixIcons.Link
    field.title.contains("用户名") || field.title.contains("账号") || field.title.contains("账户") -> MiuixIcons.Contacts
    field.title.contains("卡号") -> MiuixIcons.BankCards
    else -> MiuixIcons.Notes
}

@Composable
private fun FieldRow(
    field: EntryField,
    initiallyHidden: Boolean = field.protected,
    onCopy: (String) -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    var visible by rememberSaveable(field.title) { mutableStateOf(!initiallyHidden) }

    BasicComponent(
        title = field.title,
        summary = if (visible) field.value else "•".repeat(field.value.length.coerceAtMost(12)),
        startAction = {
            Box(
                modifier = Modifier
                    .size(36.dp)
                    .background(colors.secondaryContainer, RoundedCornerShape(50)),
                contentAlignment = Alignment.Center,
            ) {
                Icon(fieldIcon(field), null, tint = colors.onSecondaryContainer, modifier = Modifier.size(20.dp))
            }
        },
        endActions = {
            if (field.protected) {
                IconButton(onClick = { visible = !visible }) {
                    Icon(if (visible) MiuixIcons.Hide else MiuixIcons.Show, if (visible) "隐藏" else "显示")
                }
            }
            IconButton(onClick = { onCopy(field.value) }) {
                Icon(MiuixIcons.Copy, "复制")
            }
        },
    )
}

@Composable
fun EntryScreen(
    entry: KpEntry?,
    onBack: () -> Unit,
    onGeneratePassword: () -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val scrollBehavior = MiuixScrollBehavior()
    val clipboard = LocalClipboardManager.current
    var copied by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(copied) {
        if (copied != null) {
            delay(1500)
            copied = null
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = entry?.title ?: "条目",
                scrollBehavior = scrollBehavior,
                navigationIcon = {
                    IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                        Icon(MiuixIcons.Back, "返回")
                    }
                },
                actions = {
                    IconButton(onClick = onGeneratePassword, modifier = Modifier.padding(end = 12.dp)) {
                        Icon(MiuixIcons.Tune, "密码生成器")
                    }
                },
            )
        },
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize()) {
            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                contentPadding = PaddingValues(
                    start = 16.dp,
                    end = 16.dp,
                    top = padding.calculateTopPadding() + 8.dp,
                    bottom = padding.calculateBottomPadding() + 32.dp,
                ),
            ) {
                if (entry == null) {
                    item {
                        Card(modifier = Modifier.fillMaxWidth()) {
                            Text("条目不存在", modifier = Modifier.padding(24.dp))
                        }
                    }
                } else {
                    item {
                        Card(modifier = Modifier.fillMaxWidth().padding(bottom = 12.dp)) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                modifier = Modifier.padding(vertical = 10.dp),
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(56.dp)
                                        .background(colors.primaryContainer, RoundedCornerShape(16.dp)),
                                    contentAlignment = Alignment.Center,
                                ) {
                                    Icon(
                                        entry.icon,
                                        null,
                                        tint = colors.primary,
                                        modifier = Modifier.size(28.dp),
                                    )
                                }
                                Column(modifier = Modifier.padding(start = 16.dp)) {
                                    Text(entry.title, fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
                                    Spacer(Modifier.height(2.dp))
                                    Text(entry.userName, fontSize = 13.sp, color = colors.onSurfaceVariantSummary)
                                }
                            }
                        }
                    }

                    if (entry.totp != null) {
                        item {
                            Card(modifier = Modifier.fillMaxWidth().padding(bottom = 12.dp)) {
                                BasicComponent(
                                    title = "TOTP 动态验证码",
                                    summary = entry.totp,
                                    startAction = {
                                        Box(
                                            modifier = Modifier
                                                .size(36.dp)
                                                .background(colors.tertiaryContainer, RoundedCornerShape(50)),
                                            contentAlignment = Alignment.Center,
                                        ) {
                                            Icon(
                                                MiuixIcons.Timer,
                                                null,
                                                tint = colors.onTertiaryContainer,
                                                modifier = Modifier.size(20.dp),
                                            )
                                        }
                                    },
                                )
                            }
                        }
                    }

                    item { SmallTitle("字段") }
                    item {
                        Card(modifier = Modifier.fillMaxWidth()) {
                            entry.fields.forEachIndexed { index, field ->
                                FieldRow(field) { value ->
                                    clipboard.setText(AnnotatedString(value))
                                    copied = value
                                }
                                if (index != entry.fields.lastIndex) HorizontalDivider()
                            }
                        }
                    }

                    if (!entry.notes.isNullOrBlank()) {
                        item { SmallTitle("备注") }
                        item {
                            Card(modifier = Modifier.fillMaxWidth()) {
                                Text(
                                    entry.notes,
                                    color = colors.onSurface,
                                    modifier = Modifier.padding(vertical = 6.dp),
                                )
                            }
                        }
                    }
                }
            }

            AnimatedVisibility(
                visible = copied != null,
                enter = fadeIn() + expandVertically(),
                exit = fadeOut() + shrinkVertically(),
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(bottom = 24.dp),
            ) {
                Card {
                    Text("已复制：$copied", color = colors.onSurfaceContainer)
                }
            }
        }
    }
}
