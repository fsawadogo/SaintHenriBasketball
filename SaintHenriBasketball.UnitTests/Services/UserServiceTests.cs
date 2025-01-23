using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.UnitTests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var mockConfiguration = new Mock<IConfiguration>();
        var mockMapper = new Mock<IMapper>();
        var mockLogger = new Mock<ILogger<UserService>>();

        _mockUserRepo = new Mock<IUserRepository>();
        _mockEmailService = new Mock<IEmailService>();
        _userService = new UserService(mockConfiguration.Object, mockMapper.Object, _mockUserRepo.Object, _mockEmailService.Object, mockLogger.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUser()
    {
        // Arrange
        var registerDto = new RegisterUserDto
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "Password123!",
            FirstName = "Test",
            LastName = "User",
            PaymentPlan = PaymentPlan.DropIn
        };

        _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<ApplicationUser>()));
        // Act
        var result = await _userService.RegisterAsync(registerDto);

        // Assert
        Assert.Equal(registerDto.Email, result.Email);
        _mockUserRepo.Verify(r => r.AddAsync(It.IsAny<ApplicationUser>()), Times.Once);
    }
}
