public class SystemTimeResponseDto
{
    public string Mode { get; set; } = default!;
    public DateTimeOffset Now { get; set; }
    public string Timezone { get; set; } = default!;
}
