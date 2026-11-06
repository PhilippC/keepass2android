plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
}

android {
    namespace = "keepass2android.miuix.app"

    compileSdk = 37

    defaultConfig {
        applicationId = "keepass2android.miuix.prototype"
        minSdk = 26
        targetSdk = 37
        versionCode = 1
        versionName = "1.0.0"
    }

    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
            excludes += "META-INF/INDEX.LIST"
            excludes += "META-INF/versions/9/OSGI-INF/MANIFEST.MF"
        }
    }
}

dependencies {
    implementation(project(":composeApp"))
    implementation("org.jetbrains.compose.foundation:foundation:1.12.2")
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.biometric)
    // biometric:1.1.0 传递依赖 fragment:1.2.5，其 FragmentActivity.startActivityForResult
    // 会对 >0xFFFF 的 requestCode 抛出 IllegalArgumentException，导致 activity 1.13 的
    // ActivityResultContracts（随机高位 requestCode）全部无法启动，需强制升级 fragment
    implementation("androidx.fragment:fragment:1.8.9")
}
