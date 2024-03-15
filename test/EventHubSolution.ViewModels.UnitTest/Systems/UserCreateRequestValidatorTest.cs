using EventHubSolution.ViewModels.Systems;

namespace EventHubSolution.ViewModels.UnitTest.Systems
{
    public class UserCreateRequestValidatorTest
    {
        private UserCreateRequestValidator validator;
        private UserCreateRequest request;

        public UserCreateRequestValidatorTest()
        {
            request = new UserCreateRequest()
            {
                UserName = "test",
                Password = "Admin@123",
                Email = "test@gmail.com",
                PhoneNumber = "1234567890",
                FullName = "Test",
            };
            validator = new UserCreateRequestValidator();
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
        public void Should_Error_Result_When_Miss_UserName(string userName)
        {
            request.UserName = userName;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Should_Error_Result_When_Miss_Password(string data)
        {
            request.Password = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("sdasfaf")]
        [InlineData("1234567")]
        [InlineData("Admin123")]
        public void Should_Error_Result_When_Password_Not_Match(string data)
        {
            request.Password = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Should_Error_Result_When_Miss_Email(string data)
        {
            request.Email = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("sdasfaf")]
        public void Should_Error_Result_When_Email_Not_Match(string data)
        {
            request.Email = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Should_Error_Result_When_Miss_PhoneNumber(string data)
        {
            request.PhoneNumber = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Should_Error_Result_When_Miss_FullName(string data)
        {
            request.FullName = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("ogmkswqgtqjvyzmufaczuzbiowqqnrxtorbtkkzgrogmpwdfakm")]
        public void Should_Error_Result_When_FullName_Over_Length(string data)
        {
            request.FullName = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("fskgfirwqhxfgmzoxshsdkppkvjnslycwcgdtcdyknmljmiawevvlzrbzpcelfrlojhbbynisymjcduvjpovmxzpztaotmqzivvrlwhxwouagijqgrufjxiklpmhkpdwuvfigbffgcpslmlyzykrvwrfwgorzhlraoxafrpkovmhdwtdonvnlvcgbaxxihjyrqzifnsdlqxotmfrztygzoqroawgxqzigyoltxnfctpyjspbhstpdqksvoglrabkssiaiupixichkqfmvngdhzzazvbhjrqrjzhxlkrshgnujiqzgqcmzuhalcevwrfnkaelmoeypnfrsadgubzklhvnebsikmjmsdktzqawegtnfvrblzcjeaqyufhzmpcxodhzkwvnyomlseimlxssljxaepcezdxtzucbcdtfstynsmqgrksgjpgdrqhbbljmrazwelozeteaprjdtdttpcbrfplbnlzajycgopygvcuxefnyboyxhxacpdsbeerivpbabthzijcmvjbxreuxusdawnyypmxtcjdzuzuabtfkkymlrfeokwelwhkscsupzgaxplymowqpegtjhpocogsslaboreouhjwwdtwssluccifwqfnpryikvqgmjhcdtyaipwgrevmiehfiqcemtwaprqmnkuxnszopketvtckluiawenbwusnigmbvwgccgpjmwexwiwhmzirqbvydminfbzfypgbifiqyqhqxtulepnmzusjwagmtbylwjxhoocmlqpynmhtqpmbhwbucayeruzjxbuizsrudyzaxgwvyqnupuirgimvynxycpochlfhywdsoqkdduttohvrkptyhbvirktmlcogswlxilaruhyzpxozaabjkhrvkxmclzxwtmekrfeipfyldwszanbtrvkzmtiviwxyoxuroheubhjuerutcggyypccrpncatyqwdgduknuslzfdfzsqvyiiidgqdrhywftuojlrt")]
        public void Should_Error_Result_When_Bio_Over_Length(string data)
        {
            request.Bio = data;
            var result = validator.Validate(request);
            Assert.False(result.IsValid);
        }
    }
}
