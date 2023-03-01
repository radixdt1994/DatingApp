using API.Data;
using API.DTOs;
using API.Entities;
using API.Interfaces;

using AutoMapper;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Security.Cryptography;
using System.Text;

namespace API.Controllers
{
    public class AccountController : BaseApiController
    {
        private readonly DataContext _dataContext;
        private readonly ITokenService _tokenService;
        private readonly IMapper _mapper;
        public AccountController(DataContext context, ITokenService tokenService, IMapper mapper)
        {
            _dataContext = context;
            _tokenService = tokenService;
            _mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            try
            {
                if (await UserExists(registerDto.UserName))
                    return BadRequest("UserName Taken!!");

                var user = _mapper.Map<AppUser>(registerDto);

                using var hmac = new HMACSHA512();

                user.UserName = registerDto.UserName.ToLower();
                user.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password));
                user.PasswordSalt = hmac.Key;

                _dataContext.Users.Add(user);
                await _dataContext.SaveChangesAsync();

                return new UserDto
                {
                    Username = user.UserName,
                    Token = _tokenService.CreateToken(user),
                    PhotoUrl = user.Photos.FirstOrDefault(x => x.IsMain)?.Url,
                    KnownAs = user.KnownAs
                };
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user = await _dataContext.Users
                .Include(p => p.Photos)
                .SingleOrDefaultAsync(x => x.UserName == loginDto.UserName);

            if (user == null)
                return Unauthorized("Invalid Username!!");

            using var hmac = new HMACSHA512(user.PasswordSalt);

            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password));

            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != user.PasswordHash[i])
                    return Unauthorized("Invalid Password!!");
            }

            return new UserDto
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user),
                PhotoUrl = user.Photos.FirstOrDefault(x => x.IsMain)?.Url,
                KnownAs = user.KnownAs
            };
        }

        private async Task<bool> UserExists(string userName)
        {
            return await _dataContext.Users.AnyAsync(x => x.UserName == userName.ToLower());
        }
    }
}
