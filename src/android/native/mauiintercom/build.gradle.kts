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

// Create configuration for copyDependencies
configurations {
    create("copyDependencies")
}

configurations.all {
    attributes {
        attribute(Attribute.of("ui", String::class.java), "awt")
    }
}

dependencies {
    implementation(platform("androidx.compose:compose-bom:2023.10.01"))
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.material:material")
    implementation("io.intercom.android:intercom-sdk-base:15.10.1")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    runtimeOnly("com.google.code.gson:gson:2.11.0")
    runtimeOnly("com.intercom:twig:1.3.0")
    runtimeOnly("com.squareup.okhttp3:okhttp:4.12.0")
    runtimeOnly("com.squareup.retrofit2:retrofit:2.11.0")
    runtimeOnly("com.squareup.retrofit2:converter-gson:2.11.0")
    runtimeOnly("org.jetbrains.kotlinx:kotlinx-serialization-json:1.6.3")
    implementation("com.jakewharton.retrofit:retrofit2-kotlinx-serialization-converter:1.0.0")
    runtimeOnly("androidx.activity:activity-compose:1.7.2")
    runtimeOnly("androidx.appcompat:appcompat:1.6.1")
    runtimeOnly("androidx.constraintlayout:constraintlayout:2.1.4")
    runtimeOnly("androidx.core:core-ktx:1.13.1")
    runtimeOnly("androidx.exifinterface:exifinterface:1.3.7")
    runtimeOnly("androidx.fragment:fragment-ktx:1.8.2")
    runtimeOnly("androidx.lifecycle:lifecycle-viewmodel-ktx:2.8.4")
    runtimeOnly("androidx.lifecycle:lifecycle-runtime-ktx:2.8.4")
    runtimeOnly("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.4")
    runtimeOnly("androidx.navigation:navigation-compose:2.7.7")
    runtimeOnly("androidx.paging:paging-runtime-ktx:3.3.1")
    runtimeOnly("androidx.paging:paging-compose:3.3.1")
    runtimeOnly("androidx.work:work-runtime-ktx:2.9.0")
    runtimeOnly("com.facebook.shimmer:shimmer:0.5.0")
    runtimeOnly("com.google.accompanist:accompanist-systemuicontroller:0.34.0")
    runtimeOnly("com.google.accompanist:accompanist-placeholder:0.34.0")
    runtimeOnly("com.google.accompanist:accompanist-permissions:0.34.0")
    runtimeOnly("com.google.android.material:material:1.12.0")
    runtimeOnly("io.coil-kt:coil-base:2.7.0")
    runtimeOnly("io.coil-kt:coil-gif:2.7.0")
    runtimeOnly("io.coil-kt:coil-compose:2.7.0")
    runtimeOnly("io.intercom.android:intercom-sdk-ui:15.10.1")
    runtimeOnly("io.intercom.android:intercom-sdk-lightcompressor:15.10.1")
    runtimeOnly("io.intercom.android:nexus-client-android:6.3.4")
    runtimeOnly("io.sentry:sentry-android-core:7.13.0")
    "copyDependencies"("io.intercom.android:intercom-sdk-base:15.10.1")
}

// Copy dependencies for binding library
project.afterEvaluate {
    tasks.register<Copy>("copyDeps") {
        from(configurations["copyDependencies"])
        into("${buildDir}/outputs/deps")
    }
    tasks.named("preBuild") { finalizedBy("copyDeps") }
}
