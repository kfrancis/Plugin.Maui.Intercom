plugins {
    id("com.android.library")
}

android {
    namespace = "com.intercom.mauiintercom"
    compileSdk = 34

    defaultConfig {
        minSdk = 21
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
    implementation ("io.intercom.android:intercom-sdk-base:17.4.1")
    // compileOnly, not implementation: the only type needed from -ui is
    // io.intercom.android.sdk.ui.theme.ThemeMode for setThemeMode. The binding project
    // already vendors intercom-sdk-ui-17.4.1.aar, so declaring a runtime dependency here
    // would put the same classes in the graph twice.
    compileOnly ("io.intercom.android:intercom-sdk-ui:17.4.1")
}
