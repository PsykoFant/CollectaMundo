using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.Tests.UnitTests
{
    public class CommaSeparatedValuesTests
    {
        public class SplitPreservingThousandsSeparatorsTests
        {
            [Fact]
            public void Test_Splits_CommaSeparated_Values()
            {
                var result = CommaSeparatedValues.SplitPreservingThousandsSeparators("Flying, vigilance");

                Assert.Equal(["Flying", "vigilance"], result);
            }

            [Fact]
            public void Test_Does_Not_Split_Thousands_Separator()
            {
                var result = CommaSeparatedValues.SplitPreservingThousandsSeparators("10,000");

                Assert.Equal(["10,000"], result);
            }

            [Fact]
            public void Test_Splits_Values_But_Preserves_Thousands_Separator()
            {
                var result = CommaSeparatedValues.SplitPreservingThousandsSeparators("10,000, Flying");

                Assert.Equal(["10,000", "Flying"], result);
            }

            [Fact]
            public void Test_Single_Value_Is_Returned_Unchanged()
            {
                var result = CommaSeparatedValues.SplitPreservingThousandsSeparators("Flying");

                Assert.Equal(["Flying"], result);
            }

            [Fact]
            public void Test_Removes_Empty_Values()
            {
                var result = CommaSeparatedValues.SplitPreservingThousandsSeparators("Flying,, vigilance");

                Assert.Equal(["Flying", "vigilance"], result);
            }

            [Fact]
            public void Test_Trims_Whitespace_From_Values()
            {
                var result = CommaSeparatedValues.SplitPreservingThousandsSeparators(" Flying , vigilance ");

                Assert.Equal(["Flying", "vigilance"], result);
            }

            [Fact]
            public void Test_Empty_String_Returns_Empty_List()
            {
                var result = CommaSeparatedValues.SplitPreservingThousandsSeparators(string.Empty);

                Assert.Empty(result);
            }

            [Fact]
            public void Test_Whitespace_Returns_Empty_List()
            {
                var result =
                    CommaSeparatedValues.SplitPreservingThousandsSeparators(
                        "   ");

                Assert.Empty(result);
            }

            [Fact]
            public void Test_Null_Returns_Empty_List()
            {
                var result =
                    CommaSeparatedValues.SplitPreservingThousandsSeparators(
                        null);

                Assert.Empty(result);
            }
        }
    }
}
