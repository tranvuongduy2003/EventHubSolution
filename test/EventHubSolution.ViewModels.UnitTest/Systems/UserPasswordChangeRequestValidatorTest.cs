using EventHubSolution.ViewModels.Systems;

namespace EventHubSolution.ViewModels.UnitTest.Systems
{
    public class UserPasswordChangeRequestValidatorTest
    {
        private UserPasswordChangeRequestValidator validator;
        private UserPasswordChangeRequest request;

        public UserPasswordChangeRequestValidatorTest()
        {
            request = new UserPasswordChangeRequest()
            {
                OldPassword = "abcdxyz",
                NewPassword = "@Admin123"
            };
            validator = new UserPasswordChangeRequestValidator();
        }

        [Fact]
        public void Should_Valid_Result_When_Valid_Request()
        {
            var result = validator.Validate(request);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Should_Error_Result_When_Miss_OldPassword(string data)
        {
            request.OldPassword = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Should_Error_Result_When_Miss_NewPassword(string data)
        {
            request.NewPassword = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("sdasfaf")]
        [InlineData("1234567")]
        [InlineData("Admin123")]
        public void Should_Error_Result_When_NewPassword_Not_Match(string data)
        {
            request.NewPassword = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }
    }
}
