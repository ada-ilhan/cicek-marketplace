import { filtersToQuery, parseProductFilters } from "./search-params";

describe("catalogue query parameters", () => {
  it("keeps valid values", () => {
    const filters = parseProductFilters({
      q: "gül",
      categoryId: "0199d6a0-0000-7000-8000-000000000011",
      minPrice: "10.50",
      sort: "PriceAscending",
      page: "2",
      inStock: "true",
    });

    expect(filters).toMatchObject({
      q: "gül",
      minPrice: "10.50",
      sort: "PriceAscending",
      page: 2,
      inStock: true,
    });
    expect(filtersToQuery(filters, { page: 1 })).toBe(
      "q=g%C3%BCl&categoryId=0199d6a0-0000-7000-8000-000000000011&minPrice=10.50&inStock=true&sort=PriceAscending",
    );
  });

  it("drops invalid values instead of forwarding them", () => {
    const filters = parseProductFilters({ categoryId: "1 OR 1=1", minPrice: "-5", sort: "Evil", page: "-3" });

    expect(filters).toMatchObject({ categoryId: "", minPrice: "", sort: "Newest", page: 1 });
  });
});
