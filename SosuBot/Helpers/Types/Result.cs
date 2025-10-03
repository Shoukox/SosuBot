using SosuBot.Helpers.Types.Statistics;

namespace SosuBot.Helpers.Types;

public record Result<T>(T? Data, Error? Exception, bool IsSuccess);
