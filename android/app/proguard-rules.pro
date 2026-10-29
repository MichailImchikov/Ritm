-keepattributes *Annotation*, InnerClasses
-keepclassmembers @kotlinx.serialization.Serializable class ru.ritm.planner.** {
    *** Companion;
    kotlinx.serialization.KSerializer serializer(...);
}
-keep class ru.ritm.planner.data.** { *; }
