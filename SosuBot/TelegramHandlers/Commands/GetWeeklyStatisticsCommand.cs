namespace SosuBot.TelegramHandlers.Commands;

public sealed class GetWeeklyStatisticsCommand : GetDailyStatisticsCommand
{
    public new static readonly string[] Commands = ["/weekly_stats"];
    public new static readonly string Description = "показать статистику за текущую неделю";

    protected override bool UsesImplicitPeriod => true;

    protected override DateTime GetImplicitPeriodStart(DateTime today) => StartOfWeek(today);
}
