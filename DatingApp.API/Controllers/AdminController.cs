using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DatingApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IDatingRepository _repo;
        
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

        public AdminController(DataContext context, UserManager<User> userManager, IDatingRepository repo, 
                                IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _context = context;
            _userManager = userManager;
            _repo = repo;
            _cloudinaryConfig = cloudinaryConfig;

            Account acc = new Account(
                _cloudinaryConfig.Value.CloudName,
                _cloudinaryConfig.Value.ApiKey,
                _cloudinaryConfig.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(acc);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("usersWithRoles")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            //return Ok("Only admins can see this");
            var userList = await _context.Users
                .OrderBy(x => x.UserName)
                .Select(user => new 
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Roles = (from userRole in user.UserRoles
                                join role in _context.Roles
                                on userRole.RoleId equals role.Id
                                select role.Name).ToList()
                }).ToListAsync();

            return Ok(userList);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("editRoles/{userName}")]
        public async Task<IActionResult> EditRoles(string userName, RoleEditDto roleEditDto)
        {
            var user = await _userManager.FindByNameAsync(userName);

            var userRoles = await _userManager.GetRolesAsync(user);

            var selectedRoles = roleEditDto.RoleNames;

            selectedRoles = selectedRoles ?? new string[] {};
            var result = await _userManager.AddToRolesAsync(user, selectedRoles.Except(userRoles));

            if (!result.Succeeded)
                return BadRequest("Failed to add to roles");

            result = await _userManager.RemoveFromRolesAsync(user, userRoles.Except(selectedRoles));

            if (!result.Succeeded)
                return BadRequest("Failed to remove roles");

            return Ok(await _userManager.GetRolesAsync(user));


        }

        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpGet("photosForModeration")]
        public async Task<IActionResult> GetPhotosForModeration()
        {
            var photoList = await _context.Photos
                .IgnoreQueryFilters()
                .Where(p => p.IsApproved == false)
                .OrderBy(x => x.DateAdded)
                .Select(photo => new 
                {
                    Id = photo.Id,
                    UserName = photo.User.UserName,
                    PhotoUrl = photo.Url,
                    IsApproved = photo.IsApproved
                }).ToListAsync();

            return Ok(photoList);

        }

        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpPost("approvePhoto/{photoId}")]
        public async Task<IActionResult> ApprovePhoto(int photoId) 
        {
            var photoApproved = await _repo.GetPhoto(photoId);
            photoApproved.IsApproved = true;
            // var user = _repo.GetUser(photoApproved.UserId, false).Result;
            // if (!user.Photos.Any(p => p.IsMain))
            // {
            //     photoApproved.IsMain = true;
            // }

            if (await _repo.SaveAll())
                return Ok();

            return BadRequest("Could not approve the photo");

            // var photoApproved = await _context.Photos.IgnoreQueryFilters()
            //     .FirstOrDefaultAsync(p => p.Id == photoId);
            // photoApproved.IsApproved = true;

            // await _context.SaveChangesAsync();

            // return Ok();
        }

        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpPost("rejectPhoto/{photoId}")]
        public async Task<IActionResult> RejectPhoto(int photoId) 
        {
            var photoRejected = await _repo.GetPhoto(photoId);

            if (photoRejected.PublicId != null)
            {
                var deletionParams = new DeletionParams(photoRejected.PublicId);
                var deletionResult = _cloudinary.Destroy(deletionParams);

                if (deletionResult.Result == "ok")
                {
                    _repo.Delete(photoRejected);
                }
            }

            if (photoRejected.PublicId == null)
            {
                _repo.Delete(photoRejected);

            }

            if (await _repo.SaveAll())
                return Ok();

            return BadRequest("Failed to reject the photo");

        }

    }
}