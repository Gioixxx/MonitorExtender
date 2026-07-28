import java.util.Properties

plugins {
    // Da AGP 9 il supporto Kotlin e' integrato nel plugin Android: applicare anche
    // org.jetbrains.kotlin.android e' un errore, non una ridondanza.
    alias(libs.plugins.android.application)
}

// La chiave di firma non sta nel repository. Chi compila una release mette i propri dati in
// keystore.properties (ignorato da git); senza quel file la compilazione riesce lo stesso e
// produce un pacchetto non firmato, utile per verificare che R8 non rompa nulla.
val signing = Properties().apply {
    val file = rootProject.file("keystore.properties")
    if (file.exists()) file.inputStream().use { load(it) }
}
val hasSigningKey = signing.getProperty("storeFile") != null

android {
    namespace = "com.monitorextender.viewer"
    compileSdk = 36

    defaultConfig {
        // Definitivo dopo la prima pubblicazione su Play: non e' piu' modificabile.
        applicationId = "io.github.gioixxx.monitorextender"
        minSdk = 26
        targetSdk = 36
        versionCode = 1
        versionName = "1.0.0"
    }

    signingConfigs {
        create("release") {
            if (hasSigningKey) {
                storeFile = rootProject.file(signing.getProperty("storeFile"))
                storePassword = signing.getProperty("storePassword")
                keyAlias = signing.getProperty("keyAlias")
                keyPassword = signing.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            if (hasSigningKey) signingConfig = signingConfigs.getByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        viewBinding = true
    }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.activity.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.okhttp)
    implementation(libs.kotlinx.coroutines.android)

    testImplementation(libs.junit)
}
