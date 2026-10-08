namespace howmuch.Core;

public enum DutyTreatment { Representative, ConfirmedRate, SpecialTaxes }
public sealed record DutyCategory(string Id, string Label);
public sealed record DutyProfile(string Id, string Category, string Label, decimal? Rate,
    GoodsType? GoodsType, string Explanation, string SourceUrl);
public sealed record DutyAdvice(decimal? Rate, GoodsType? GoodsType, string Explanation, string SourceUrl)
{
    public bool CanApply => Rate.HasValue;
}

public static class DutyAdvisor
{
    public const string CalculatorUrl = "https://www.customs.go.kr/kcs/ad/tax/BuyTaxCalculation.do";
    private const string TextileSource = "https://www.customs.go.kr/download/ftaportalkor/_down/trty/han_sg_05.pdf";
    private const string ShoeSource = "https://www.customs.go.kr/download/ftaportalkor/_down/trty/han_eu_1_05.pdf";
    private const string WarmerSource = "https://unipass.customs.go.kr/clip/hsinfosrch/openULS0201007Q.do?opnMod=P&searchVal=8516799000";

    public static IReadOnlyList<DutyCategory> Categories { get; } =
    [new("clothes", "의류"), new("shoes", "신발"), new("electronics", "전자·생활용품"),
     new("food", "식품·건강기능식품"), new("other", "기타 / 잘 모르겠어요")];

    // Representative classification examples, not an exhaustive tariff table or live tariff lookup.
    // Treaty schedules provide historical base-rate examples; confirm current assessment in CLIP.
    public static IReadOnlyList<DutyProfile> Profiles { get; } =
    [
        new("cotton-shirt", "clothes", "면으로 만든 편물 티셔츠", 13, GoodsType.ListEligible,
            "면 편물 티셔츠의 대표 기준세율 13%를 참고합니다. 소재·제조 방식에 따라 분류가 달라질 수 있습니다.", TextileSource),
        new("knit", "clothes", "양모·면 니트 풀오버 / 가디건", 13, GoodsType.ListEligible,
            "니트 의류의 대표 기준세율 13%를 참고합니다. 유아복·특수 의류에는 그대로 적용하지 마세요.", TextileSource),
        new("clothes-other", "clothes", "유아복 / 기타 의류 / 소재를 모름", null, null,
            "유아복·특수 의류는 별도 분류가 필요합니다. 소재와 용도를 관세청 계산기에서 확인하세요.", CalculatorUrl),
        new("sports-shoe", "shoes", "섬유 갑피·고무/플라스틱 바닥 운동화", 13, GoodsType.ListEligible,
            "이 구성의 운동화는 대표 기준세율 13%를 참고합니다. 갑피·바닥 소재가 다르면 재확인이 필요합니다.", ShoeSource),
        new("rubber-boot", "shoes", "갑피와 바닥이 고무인 방수 장화", 8, GoodsType.ListEligible,
            "고무 방수 장화의 대표 기준세율 8%를 참고합니다. 일반 운동화에는 이 세율을 적용하지 마세요.", ShoeSource),
        new("shoes-other", "shoes", "기타 신발 / 소재를 모름", null, null,
            "갑피·바닥 소재와 용도를 확인해야 관세율을 고를 수 있습니다.", CalculatorUrl),
        new("warmer", "electronics", "전기찜질기·발보온기·전기손난로", 8, GoodsType.ListEligible,
            "HS 8516.79-9000에 해당하는 전열기구의 기본세율 8%를 참고합니다. 다른 전자제품의 세율은 다릅니다.", WarmerSource),
        new("electronics-other", "electronics", "컴퓨터 / 기타 전자·생활용품", null, null,
            "전자제품마다 무관세 또는 다른 세율이 적용됩니다. 제품의 정확한 용도와 품목번호를 확인하세요.", CalculatorUrl),
        new("supplement", "food", "영양제 / 건강기능식품", null, GoodsType.ListExcluded,
            "성분·제형에 따라 세율과 반입 요건이 달라집니다. 목록통관 제외 물품이며 미국 발송이어도 면세 기준은 150달러입니다.", CalculatorUrl),
        new("food-other", "food", "일반 식품 / 농축수산물", null, GoodsType.ListExcluded,
            "식품 전체에 같은 관세율을 적용할 수 없습니다. 원재료·가공 방식과 추가 세금을 관세청에서 확인하세요.", CalculatorUrl),
        new("other", "other", "품목을 직접 확인할게요", null, null,
            "정확한 품목을 확인할 수 없어 관세율을 제안하지 않습니다. 관세청에서 확인한 세율을 입력할 수 있습니다.", CalculatorUrl)
    ];

    public static DutyAdvice Estimate(string? category, string? profileId, DutyTreatment treatment, string confirmedRate)
    {
        var profile = Profiles.SingleOrDefault(p => p.Id == profileId && p.Category == category);
        if (profile is null) return new(null, null, "물품 종류와 세부 품목을 선택하세요.", CalculatorUrl);
        if (!Enum.IsDefined(treatment)) return new(null, null, "감면 세율이나 특별세 여부를 선택하세요.", CalculatorUrl);
        if (treatment == DutyTreatment.SpecialTaxes)
            return new(null, null, "주류·담배·고가품 등의 특별세는 이 위젯에서 계산하지 않습니다. 관세청의 전체 세액 계산을 이용하세요.", CalculatorUrl);
        if (treatment == DutyTreatment.ConfirmedRate)
        {
            if (!ImportCalculator.TryAmount(confirmedRate, out decimal rate) || rate > 100)
                return new(null, null, "공식 확인한 관세율을 0~100%로 입력하세요. 발송지만으로 FTA 무관세를 판단하지 않습니다.", CalculatorUrl);
            return new(rate, profile.GoodsType,
                "직접 확인한 적용 관세율을 사용합니다. FTA 세율은 해당 품목의 원산지·증빙·협정 요건을 충족해야 합니다.", CalculatorUrl);
        }
        return new(profile.Rate, profile.GoodsType, profile.Explanation +
            (profile.Rate.HasValue ? " 대표 참고값이며 최신 적용세율·FTA는 관세청에서 재확인하세요. 관세율 0%여도 부가세가 생길 수 있습니다." : ""), profile.SourceUrl);
    }
}
