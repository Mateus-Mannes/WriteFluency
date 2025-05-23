using Bogus;

namespace WriteFluency.Propositions;

public static class NewsDtoFaker
{
    public static IEnumerable<NewsDto> Generate(int quantity = 1)
    {
        var faker = new Faker();
        for (int i = 0; i < quantity; i++)
        {
            yield return new NewsDto(
                faker.Random.Guid().ToString(),
                faker.Lorem.Sentence(),
                faker.Lorem.Paragraph(),
                faker.Internet.Url(),
                faker.Internet.Url()
            );
        }
    }
}
