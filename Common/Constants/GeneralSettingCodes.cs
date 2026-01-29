namespace presensi_kpu_batu_be.Common.Constants;

public static class GeneralSettingCodes
{
    public const string WORKING_START_TIME = "WORKING_START_TIME";
    public const string WORKING_END_TIME = "WORKING_END_TIME";
    public const string LATE_TOLERANCE_MINUTES = "LATE_TOLERANCE_MINUTES";
    public const string GEOFENCE_RADIUS_METER = "GEOFENCE_RADIUS_METER";
    public const string TIMEZONE = "TIMEZONE";
    public const string EARLY_LEAVE_TOLERANCE_MINUTES = "EARLY_LEAVE_TOLERANCE_MINUTES";

    // Combined/legacy keys
    public const string LATITUDE_LONGITUDE = "LATITUDE_LONGITUDE"; // format: "lat, lon"
    public const string MAX_RADIUS = "MAX_RADIUS"; // radius in meters

    // Toggle to enable/disable geofence validation
    public const string IS_LOCATION_GEOFENCE_ENABLED = "IS_LOCATION_GEOFENCE_ENABLED"; // values: true/false
}
