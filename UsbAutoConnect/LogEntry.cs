namespace UsbAutoConnect;

public sealed record LogEntry(string Time, string Level, string Message, string Color, string Background)
{
    public static LogEntry Create(string text)
    {
        var (level, color, background) = text.Contains("失败") || text.Contains("未保存")
            ? ("错误", "#C12B30", "#FDEBED")
            : text.Contains("不自动连接") || text.Contains("已停止") || text.Contains("未找到")
                ? ("提醒", "#995900", "#FFF0D6")
                : text.Contains("完成") || text.Contains("已确认连接") || text.Contains("已恢复")
                    ? ("成功", "#18713D", "#E5F5EA")
                    : text.Contains("连接请求") || text.Contains("已开始") || text.Contains("已受理")
                        ? ("操作", "#0862C7", "#E6F0FF")
                        : ("信息", "#4F5563", "#EFF1F5");
        return new(DateTime.Now.ToString("HH:mm:ss"), level, text, color, background);
    }
}
