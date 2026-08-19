plugins {
    id("com.android.library")
}

android {
    namespace = "com.intercom.mauiintercom"
    // Intercom Android 18.0.0 requires compileSdk 36 or higher and minSdk 23; below either,
    // its AAR's aar-metadata.properties fails the Gradle compatibility check outright.
    compileSdk = 36

    defaultConfig {
        minSdk = 23
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_1_8
        targetCompatibility = JavaVersion.VERSION_1_8
    }
}

dependencies {
    // Use intercom-sdk-base which matches the AAR files in the binding project
    // The UI and other dependencies are provided via the binding project's AndroidLibrary items
    implementation ("io.intercom.android:intercom-sdk-base:18.8.0")
    // compileOnly, not implementation: the only type needed from -ui is
    // io.intercom.android.sdk.ui.theme.ThemeMode for setThemeMode. The binding project
    // already vendors intercom-sdk-ui-18.8.0.aar, so declaring a runtime dependency here
    // would put the same classes in the graph twice.
    compileOnly ("io.intercom.android:intercom-sdk-ui:18.8.0")
}
