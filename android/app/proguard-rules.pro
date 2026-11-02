-keepattributes *Annotation*, InnerClasses
-keepclassmembers @kotlinx.serialization.Serializable class ru.ritm.planner.** {
    *** Companion;
    kotlinx.serialization.KSerializer serializer(...);
}
-keep class ru.ritm.planner.data.** { *; }
# SMB-клиент находит обработчики событий и криптографию через отражение.
-keep class com.hierynomus.** { *; }
-keep class net.engio.mbassy.** { *; }
-keep class org.bouncycastle.** { *; }
-dontwarn com.hierynomus.**
-dontwarn net.engio.mbassy.**
-dontwarn org.bouncycastle.**
-dontwarn org.slf4j.**
-dontwarn javax.el.**
-dontwarn javax.naming.**
-dontwarn org.ietf.jgss.**
