plugins {
    // Da AGP 9 il supporto Kotlin e' integrato nel plugin Android: applicare anche
    // org.jetbrains.kotlin.android e' un errore, non una ridondanza.
    alias(libs.plugins.android.application)
}

android {
    namespace = "com.monitorextender.viewer"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.monitorextender.viewer"
        minSdk = 26
        targetSdk = 36
        versionCode = 1
        versionName = "0.1"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
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
