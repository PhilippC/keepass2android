import org.jetbrains.compose.desktop.application.dsl.TargetFormat

plugins {
    alias(libs.plugins.android.kmp.library)
    alias(libs.plugins.kotlin.multiplatform)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.compose.multiplatform)
}

kotlin {
    android {
        namespace = "keepass2android.miuix"
        compileSdk = 37
        minSdk = 26
    }

    jvm("desktop")

    sourceSets {
        val commonMain by getting {
            dependencies {
                implementation(libs.miuix.ui)
                implementation(libs.miuix.icons)
                implementation(libs.miuix.preference)
            }
        }

        val jvmShared by creating {
            dependsOn(commonMain)
        }
        androidMain.get().dependsOn(jvmShared)

        val desktopMain by getting {
            dependsOn(jvmShared)
            dependencies {
                implementation(compose.desktop.currentOs)
            }
        }
    }
}

compose.desktop {
    application {
        mainClass = "keepass2android.miuix.MainKt"

        nativeDistributions {
            targetFormats(TargetFormat.Dmg, TargetFormat.Msi, TargetFormat.Deb)
            packageName = "kp2a-miuix"
            packageVersion = "1.0.0"
        }
    }
}
