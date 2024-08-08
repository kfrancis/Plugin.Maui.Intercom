# Add project specific ProGuard rules here.
# You can control the set of applied configuration files using the
# proguardFiles setting in build.gradle.
#
# For more details, see
#   http://developer.android.com/guide/developing/tools/proguard.html

# If your project uses WebView with JS, uncomment the following
# and specify the fully qualified class name to the JavaScript interface
# class:
#-keepclassmembers class fqcn.of.javascript.interface.for.webview {
#   public *;
#}

# Uncomment this to preserve the line number information for
# debugging stack traces.
#-keepattributes SourceFile,LineNumberTable

# If you keep the line number information, uncomment this to
# hide the original source file name.
#-renamesourcefileattribute SourceFile

# Keep all classes in the Intercom SDK
-keep class io.intercom.android.** { *; }
-keep class com.intercom.** { *; }

# Explicitly keep the IntercomStatusCallback interface
-keep interface io.intercom.android.sdk.IntercomStatusCallback { *; }

# Keep all members of classes implementing this interface
-keepclassmembers class * implements io.intercom.android.sdk.IntercomStatusCallback { *; }

# Keep annotations
-keepattributes *Annotation*

# Keep line numbers and source file names for debugging purposes
-renamesourcefileattribute SourceFile
-keepattributes SourceFile,LineNumberTable
