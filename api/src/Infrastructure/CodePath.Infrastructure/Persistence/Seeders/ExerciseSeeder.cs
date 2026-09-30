using CodePath.Domain.Exercises.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Persistence.Seeders;

public static class ExerciseSeeder
{
    public static async Task SeedDemoExerciseAsync(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.Exercises.AnyAsync(exercise => exercise.Slug == "two-sum", cancellationToken))
            return;

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var exercise = Exercise.Create(
            "two-sum",
            "Two Sum",
            "Easy",
            "Cho một mảng số nguyên `nums` và một số nguyên `target`. Hãy in ra chỉ số của hai phần tử có tổng bằng `target`.\n\nBạn có thể giả sử mỗi dữ liệu vào có đúng một đáp án và không được dùng cùng một phần tử hai lần. In chỉ số nhỏ hơn trước, cách nhau bởi một dấu cách.",
            "2 ≤ n ≤ 10⁴\n-10⁹ ≤ nums[i], target ≤ 10⁹\nLuôn tồn tại đúng một đáp án.",
            "n, target = map(int, input().split())\nnums = list(map(int, input().split()))\n\n# Viết lời giải của bạn tại đây\n",
            2_000,
            128_000,
            utcNow);

        exercise.AddTestCase("4 9\n2 7 11 15\n", "0 1\n", true, 1,
            "nums[0] + nums[1] = 2 + 7 = 9.", utcNow);
        exercise.AddTestCase("3 6\n3 2 4\n", "1 2\n", true, 2,
            "nums[1] + nums[2] = 2 + 4 = 6.", utcNow);
        exercise.AddTestCase("2 6\n3 3\n", "0 1\n", false, 3, null, utcNow);
        exercise.AddTestCase("5 -8\n-3 4 -5 8 1\n", "0 2\n", false, 4, null, utcNow);
        exercise.Publish(utcNow);

        dbContext.Exercises.Add(exercise);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded demo exercise {ExerciseSlug}.", exercise.Slug);
    }
}
