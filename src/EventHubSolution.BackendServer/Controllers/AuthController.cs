﻿using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Systems;
using IdentityModel.Client;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _emailService;

        public AuthController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, SignInManager<User> signInManager, ITokenService tokenService, ApplicationDbContext db, IEmailService emailService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _db = db;
            _emailService = emailService;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] UserCreateRequest request)
        {
            var useByEmail = await _userManager.FindByEmailAsync(request.Email);
            if (useByEmail != null)
                return BadRequest(new ApiBadRequestResponse("Email already exists"));

            var useByPhoneNumber = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (useByPhoneNumber != null)
                return BadRequest(new ApiBadRequestResponse("Phone number already exists"));

            User user = new()
            {
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                FullName = request.FullName,
                Status = UserStatus.ACTIVE
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                var userToReturn = await _userManager.FindByEmailAsync(request.Email);

                await _userManager.AddToRolesAsync(user, new List<string>
                {
                    UserRole.CUSTOMER.GetDisplayName(),
                    UserRole.ORGANIZER.GetDisplayName()
                });

                await _signInManager.PasswordSignInAsync(user, request.Password, false, false);

                //TODO: If user was found, generate JWT Token
                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = await _userManager.GenerateUserTokenAsync(user, TokenProviders.DEFAULT, TokenTypes.REFRESH);

                await _userManager.SetAuthenticationTokenAsync(user, TokenProviders.DEFAULT, TokenTypes.REFRESH, refreshToken);

                SignInResponse signUpResponse = new SignInResponse()
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                };

                await SendRegistrationConfirmationEmailAsync(userToReturn.Email, userToReturn.UserName);

                return CreatedAtAction("POST", new { id = userToReturn.Id }, signUpResponse);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(result));
            }
        }

        [HttpPost("signin")]
        public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
        {
            var user = _userManager.Users.FirstOrDefault(u => u.Email == request.Identity || u.PhoneNumber == request.Identity);

            if (user == null)
                return NotFound(new ApiNotFoundResponse("Email or phone number does not exist"));

            if (user.Status == UserStatus.INACTIVE)
            {
                return Unauthorized(new ApiUnauthorizedResponse("Your account is disabled"));
            }

            bool isValid = await _userManager.CheckPasswordAsync(user, request.Password);

            if (isValid == false)
            {
                return Unauthorized(new ApiUnauthorizedResponse("Wrong password"));
            }

            await _signInManager.PasswordSignInAsync(user, request.Password, false, false);

            //TODO: If user was found, generate JWT Token
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = await _userManager.GenerateUserTokenAsync(user, TokenProviders.DEFAULT, TokenTypes.REFRESH);

            await _userManager.SetAuthenticationTokenAsync(user, TokenProviders.DEFAULT, TokenTypes.REFRESH, refreshToken);

            SignInResponse signInResponse = new SignInResponse()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var options = new CookieOptions()
            {
                //Domain = settings.AppDomain,
                Domain = "http://localhost:5173/",
                Expires = DateTime.UtcNow.AddMinutes(5)
            };

            Response.Cookies.Append(
                "AuthTokenHolder",
                JsonConvert.SerializeObject(signInResponse, new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    },
                    Formatting = Formatting.Indented
                }), options);

            return Ok(signInResponse);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("external-login")]
        public IActionResult ExternalLogin(string provider, string returnUrl)
        {
            var redirectUrl = $"https://900b-112-78-7-6.ngrok-free.app/api/Auth/external-auth-callback";
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            properties.AllowRefresh = true;
            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("external-auth-callback")]
        public async Task<IActionResult> ExternalLoginCallback()
        {
            ExternalLoginInfo info = await _signInManager.GetExternalLoginInfoAsync();

            SignInResponse signInResponse = new();

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (!email.IsNullOrEmpty())
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    user = new User()
                    {
                        UserName = email,
                        Email = email,
                        PhoneNumber = info.Principal.FindFirstValue(ClaimTypes.MobilePhone),
                        FullName = info.Principal.FindFirstValue(ClaimTypes.Name),
                        Status = UserStatus.ACTIVE
                    };

                    var result = await _userManager.CreateAsync(user);

                    if (result.Succeeded)
                    {
                        await _userManager.AddToRolesAsync(user, new List<string>
                        {
                            UserRole.CUSTOMER.GetDisplayName(),
                            UserRole.ORGANIZER.GetDisplayName()
                        });
                    }
                    else
                    {
                        return BadRequest(new ApiBadRequestResponse(result));
                    }

                    await SendRegistrationConfirmationEmailAsync(user.Email, user.UserName);
                }

                await _signInManager.SignInAsync(user, false);

                //TODO: Generate JWT Token
                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = await _userManager.GenerateUserTokenAsync(user, info.LoginProvider, TokenTypes.REFRESH);

                await _userManager.SetAuthenticationTokenAsync(user, info.LoginProvider, TokenTypes.REFRESH, refreshToken);

                signInResponse = new SignInResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                };
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse("Sign In Failed"));
            }

            var options = new CookieOptions()
            {
                //Domain = settings.AppDomain,
                Domain = "http://localhost:5174/",
                Expires = DateTime.UtcNow.AddMinutes(5)
            };

            Response.Cookies.Append(
                "AuthTokenHolder",
                JsonConvert.SerializeObject(signInResponse, new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    },
                    Formatting = Formatting.Indented
                }), options);

            return Redirect($"http://localhost:5174/");
        }

        [Authorize]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (request is null || request.RefreshToken is null || request.RefreshToken == "")
                return BadRequest("Invalid token");

            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (accessToken == null || accessToken == "")
            {
                return Unauthorized(new ApiUnauthorizedResponse("Unauthorized"));
            }
            var principal = _tokenService.GetPrincipalFromToken(accessToken);

            var user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            if (user == null)
            {
                return Unauthorized(new ApiUnauthorizedResponse("Unauthorized"));
            }

            var isValid = await _userManager.VerifyUserTokenAsync(user, TokenProviders.DEFAULT, TokenTypes.REFRESH, request.RefreshToken);
            if (!isValid)
            {
                return Unauthorized(new ApiUnauthorizedResponse("Unauthorized"));
            }

            var newAccessToken = _tokenService.GenerateAccessToken(user);
            var newRefreshToken = await _userManager.GenerateUserTokenAsync(user, TokenProviders.DEFAULT, TokenTypes.REFRESH);

            SignInResponse refreshResponse = new SignInResponse()
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };

            return Ok(refreshResponse);
        }

        [Authorize]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetPasswordUrl = $"https://localhost:5173/reset-password?token={token}&email={request.Email}";
                await SendResetPasswordEmailAsync(request.Email, resetPasswordUrl);
            }

            return Ok();
        }

        [Authorize]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromQuery] UserResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user == null)
            {
                return NotFound(new ApiNotFoundResponse("User does not exist"));
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new ApiBadRequestResponse(result));
            }

            return Ok();
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (accessToken == null || accessToken == "")
            {
                return Unauthorized(new ApiUnauthorizedResponse("Unauthorized"));
            }
            var principal = _tokenService.GetPrincipalFromToken(accessToken);

            var user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            if (user == null)
            {
                return Unauthorized(new ApiUnauthorizedResponse("Unauthorized"));
            }

            var avatar = await _db.FileStorages.FindAsync(user.AvatarId);
            var roles = await _userManager.GetRolesAsync(user);
            var userVm = new UserVm()
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Dob = user.Dob,
                FullName = user.FullName,
                Gender = user.Gender,
                Bio = user.Bio,
                NumberOfCreatedEvents = user.NumberOfCreatedEvents,
                NumberOfFavourites = user.NumberOfFavourites,
                NumberOfFolloweds = user.NumberOfFolloweds,
                NumberOfFollowers = user.NumberOfFollowers,
                Status = user.Status,
                Avatar = avatar.FilePath,
                Roles = roles.ToList(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
            return Ok(userVm);
        }

        private async Task SendRegistrationConfirmationEmailAsync(string email, string userName)
        {
            string FullPath = Path.Combine("Templates/", "SignUpEmailTemplate.html");

            StreamReader str = new StreamReader(FullPath);

            string mailText = str.ReadToEnd();

            str.Close();

            mailText = mailText.Replace("[userName]", userName);

            await _emailService.SendEmailAsync(email, "Registration Confirmation", mailText);
        }

        private async Task SendResetPasswordEmailAsync(string email, string resetPasswordUrl)
        {
            string FullPath = Path.Combine("Templates/", "ResetPasswordEmailTemplate.html");

            StreamReader str = new StreamReader(FullPath);

            string mailText = str.ReadToEnd();

            str.Close();

            mailText = mailText.Replace("[resetPasswordUrl]", resetPasswordUrl);

            await _emailService.SendEmailAsync(email, "Reset Your Password", mailText);
        }
    }
}
