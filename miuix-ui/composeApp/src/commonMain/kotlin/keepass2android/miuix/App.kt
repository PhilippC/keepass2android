package keepass2android.miuix

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import top.yukonga.miuix.kmp.basic.NavigationBar
import top.yukonga.miuix.kmp.basic.NavigationBarItem
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Favorites
import top.yukonga.miuix.kmp.icon.extended.GridView
import top.yukonga.miuix.kmp.icon.extended.Settings
import top.yukonga.miuix.kmp.theme.MiuixTheme
import top.yukonga.miuix.kmp.theme.darkColorScheme
import top.yukonga.miuix.kmp.theme.lightColorScheme
import keepass2android.miuix.data.DemoData
import keepass2android.miuix.data.KpEntry
import keepass2android.miuix.data.RecentDatabase
import keepass2android.miuix.onedrive.OneDriveDefaults
import keepass2android.miuix.screens.EntryScreen
import keepass2android.miuix.screens.FavoritesScreen
import keepass2android.miuix.screens.GeneratePasswordScreen
import keepass2android.miuix.screens.GroupScreen
import keepass2android.miuix.screens.OneDriveScreen
import keepass2android.miuix.screens.SettingsScreen
import keepass2android.miuix.screens.StartScreen
import keepass2android.miuix.screens.UnlockScreen

sealed interface Screen {
    data object Start : Screen
    data class Unlock(val database: RecentDatabase, val keyFileName: String? = null) : Screen
    data object OneDrive : Screen
    data class Group(val groupId: Int) : Screen
    data object Favorites : Screen
    data class Entry(val entryId: Int) : Screen
    data object GeneratePassword : Screen
    data object Settings : Screen
}

enum class ThemeMode(val label: String) {
    System("跟随系统"), Light("浅色"), Dark("深色"),
}

private data class Tab(val screen: Screen, val label: String, val icon: androidx.compose.ui.graphics.vector.ImageVector)

private val tabs = listOf(
    Tab(Screen.Group(0), "数据库", MiuixIcons.GridView),
    Tab(Screen.Favorites, "收藏", MiuixIcons.Favorites),
    Tab(Screen.Settings, "设置", MiuixIcons.Settings),
)

@Composable
fun App(cacheDirPath: String, oneDriveClientId: String, onOpenUrl: (String) -> Unit = {}) {
    val effectiveOneDriveClientId = oneDriveClientId.ifBlank { OneDriveDefaults.ClientId }
    val systemDark = isSystemInDarkTheme()
    var themeMode by remember { mutableStateOf(ThemeMode.System) }
    val isDark = when (themeMode) {
        ThemeMode.System -> systemDark
        ThemeMode.Light -> false
        ThemeMode.Dark -> true
    }

    val backStack = remember { mutableStateListOf<Screen>(Screen.Start) }
    val current = backStack.last()
    val extraEntries = remember { mutableStateListOf<KpEntry>() }

    val isTabLevel = current is Screen.Group || current is Screen.Favorites || current is Screen.Settings
    val tabIndex = tabs.indexOfFirst { it.screen == current }

    fun navigate(screen: Screen) {
        backStack.add(screen)
    }

    fun back() {
        if (backStack.size > 1) backStack.removeAt(backStack.lastIndex)
    }

    fun switchTab(tab: Screen) {
        if (current == tab) return
        val tabBase = backStack.indexOfFirst {
            it is Screen.Group || it is Screen.Favorites || it is Screen.Settings
        }
        if (tabBase >= 0) {
            while (backStack.size > tabBase + 1) backStack.removeAt(backStack.lastIndex)
            backStack[tabBase] = tab
        } else {
            navigate(tab)
        }
    }

    fun depthOf(screen: Screen): Int = backStack.indexOf(screen).coerceAtLeast(0)

    MiuixTheme(
        colors = if (isDark) darkColorScheme() else lightColorScheme(),
    ) {
        Column(modifier = Modifier.fillMaxSize()) {
            Box(modifier = Modifier.weight(1f)) {
                AnimatedContent(
                    targetState = current,
                    transitionSpec = {
                        val pushing = depthOf(targetState) >= depthOf(initialState)
                        if (pushing) {
                            (slideInHorizontally(tween(280)) { it / 4 } + fadeIn(tween(280)))
                                .togetherWith(slideOutHorizontally(tween(280)) { -it / 6 } + fadeOut(tween(200)))
                        } else {
                            (slideInHorizontally(tween(280)) { -it / 6 } + fadeIn(tween(280)))
                                .togetherWith(slideOutHorizontally(tween(280)) { it / 4 } + fadeOut(tween(200)))
                        }
                    },
                    label = "screen",
                ) { screen ->
                    when (screen) {
                        Screen.Start -> StartScreen(
                            onOpenDatabase = { navigate(Screen.Unlock(DemoData.recentDatabases.first())) },
                            onOpenOneDrive = { navigate(Screen.OneDrive) },
                            onSettings = { navigate(Screen.Settings) },
                        )

                        Screen.OneDrive -> OneDriveScreen(
                            cacheDirPath = cacheDirPath,
                            clientId = effectiveOneDriveClientId,
                            onOpenUrl = onOpenUrl,
                            onBack = ::back,
                            onFetched = { database, keyFileName ->
                                navigate(Screen.Unlock(database, keyFileName))
                            },
                        )

                        is Screen.Unlock -> UnlockScreen(
                            database = screen.database,
                            keyFileName = screen.keyFileName,
                            onBack = ::back,
                            onUnlocked = { navigate(Screen.Group(0)) },
                        )

                        is Screen.Group -> GroupScreen(
                            groupId = screen.groupId,
                            extraEntries = extraEntries.toList(),
                            onBack = ::back,
                            onOpenGroup = { navigate(Screen.Group(it)) },
                            onOpenEntry = { navigate(Screen.Entry(it)) },
                            onAddEntry = {
                                extraEntries.add(
                                    KpEntry(
                                        id = 1000 + extraEntries.size,
                                        groupId = screen.groupId,
                                        title = "新建条目 ${extraEntries.size + 1}",
                                        userName = "new.user@example.com",
                                        icon = MiuixIcons.GridView,
                                        fields = emptyList(),
                                    ),
                                )
                            },
                            onGeneratePassword = { navigate(Screen.GeneratePassword) },
                        )

                        Screen.Favorites -> FavoritesScreen(
                            extraEntries = extraEntries.toList(),
                            onBack = ::back,
                            onOpenEntry = { navigate(Screen.Entry(it)) },
                        )

                        is Screen.Entry -> EntryScreen(
                            entry = DemoData.findEntry(screen.entryId)
                                ?: extraEntries.firstOrNull { it.id == screen.entryId },
                            onBack = ::back,
                            onGeneratePassword = { navigate(Screen.GeneratePassword) },
                        )

                        Screen.GeneratePassword -> GeneratePasswordScreen(onBack = ::back)

                        Screen.Settings -> SettingsScreen(
                            themeMode = themeMode,
                            onThemeModeChange = { themeMode = it },
                            databasePath = DemoData.recentDatabases.first().path,
                        )
                    }
                }
            }

            if (isTabLevel) {
                NavigationBar {
                    tabs.forEachIndexed { index, tab ->
                        NavigationBarItem(
                            selected = index == tabIndex,
                            onClick = { switchTab(tab.screen) },
                            icon = tab.icon,
                            label = tab.label,
                        )
                    }
                }
            }
        }
    }
}
