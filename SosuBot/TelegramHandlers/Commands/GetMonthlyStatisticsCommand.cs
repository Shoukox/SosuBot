namespace SosuBot.TelegramHandlers.Commands;

public sealed class GetMonthlyStatisticsCommand : GetDailyStatisticsCommand
{
    public new static readonly string[] Commands = ["/monthly_stats"];
    public new static readonly string Description = "показать статистику за текущий месяц";

    protected override bool UsesImplicitPeriod => true;

    protected override DateTime GetImplicitPeriodStart(DateTime today) => new(today.Year, today.Month, 1);
}
