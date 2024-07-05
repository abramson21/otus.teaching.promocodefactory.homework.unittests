using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Otus.Teaching.PromoCodeFactory.Core.Abstractions.Repositories;
using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;
using Otus.Teaching.PromoCodeFactory.DataAccess;
using Otus.Teaching.PromoCodeFactory.WebHost.Controllers;
using Otus.Teaching.PromoCodeFactory.WebHost.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Otus.Teaching.PromoCodeFactory.UnitTests.WebHost.Controllers.Partners
{
    public class SetPartnerPromoCodeLimitAsyncTests
    {
        private readonly Mock<IRepository<Partner>> partnersRepositoryMock;
        private readonly PartnersController partnersController;
        private readonly IFixture fixture;

        public SetPartnerPromoCodeLimitAsyncTests()
        {
            fixture = new Fixture().Customize(new AutoMoqCustomization());

            partnersRepositoryMock = fixture.Freeze<Mock<IRepository<Partner>>>();

            partnersController = fixture
                .Build<PartnersController>()
                .OmitAutoProperties()
                .Create();
        }


        /// <summary>
        /// Тест на отсутсвие определенного партнер по идентификатору.
        /// </summary>
        /// <returns> Асинхронное выполнение. </returns>
        [Fact]
        public async Task GetPartnerById_ReturnNotFound404_Success()
        {
            // Arrange
            var setRequest = fixture
                .Build<SetPartnerPromoCodeLimitRequest>()
                .Create();

            partnersRepositoryMock
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(null as Partner);

            var partnersController = fixture
                .Build<PartnersController>()
                .OmitAutoProperties()
                .Create();

            // Act
            var result = await partnersController
                .SetPartnerPromoCodeLimitAsync(default, setRequest);

            // Assert
            result.Should().BeAssignableTo<NotFoundResult>();
        }

        /// <summary>
        /// Тест на блокировку партнера (IsActive=false).
        /// </summary>
        /// <returns> Асинхронное выполнение. </returns>
        [Fact]
        public async Task CheckActivePartner_ReturnBadRequest_Success()
        {
            // Arrange
            var partnerId = Guid.NewGuid();

            var partner = fixture.Build<Partner>()
                .OmitAutoProperties()
                .With(s => s.IsActive, false)
                .With(s => s.Id, partnerId)
                .Create();

            var setPartnerPromoCodeLimitRequest = new SetPartnerPromoCodeLimitRequest
            {
                EndDate = DateTime.Now,
                Limit = 10,
            };

            partnersRepositoryMock
                .Setup(x => x.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            // Act
            var result = await partnersController
                .SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            result.Should().BeAssignableTo<BadRequestObjectResult>();
        }

        /// <summary>
        /// Обновление лимита промокодов партнера.
        /// Лимит установлен, значит обнуляем количество промокодов.
        /// </summary>
        /// <returns> Асинхронная выполнение. </returns>
        [Fact]
        public async Task UpdateNumberIssuedPromoCodes_LimitPromoCodeIsZero_Success()
        {
            // Arrange
            var partnerId = Guid.NewGuid();

            var partnerPromoCodeLimit = fixture
                .Build<PartnerPromoCodeLimit>()
                .OmitAutoProperties()
                .With(x => x.Limit, 20)
                .Create();

            var partner = fixture
                .Build<Partner>()
                .OmitAutoProperties()
                .With(x => x.Id, partnerId)
                .With(x => x.IsActive, true)
                .With(x => x.NumberIssuedPromoCodes, 10)
                .With(x => x.PartnerLimits, new List<PartnerPromoCodeLimit>() { partnerPromoCodeLimit })
                .Create();

            var setPartnerPromoCodeLimitRequest = this.fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .OmitAutoProperties()
                .With(x => x.EndDate, DateTime.Now)
                .With(x => x.Limit, 10)
                .Create();

            partnersRepositoryMock
                .Setup(x => x.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            // Act
            var result = await partnersController.SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            partner.NumberIssuedPromoCodes.Should().Be(0);
            partnersRepositoryMock.Verify(s => s.UpdateAsync(partner), Times.Once);
            result.Should().BeOfType<CreatedAtActionResult>();
        }

        /// <summary>
        /// Тест на добавление нового лимита (необходимо удалить старый лимит).
        /// </summary>
        /// <returns> Асинхронное выполнение. </returns>
        [Fact]
        public async Task AddNewLimit_AddLimitShotdownOldLimit_Success()
        {
            // Arrange
            var limit = fixture.Build<PartnerPromoCodeLimit>()
                .OmitAutoProperties()
                .With(x => x.CancelDate, (DateTime?)null)
                .Create();

            var partner = fixture.Build<Partner>()
                .OmitAutoProperties()
                .With(x => x.PartnerLimits, [limit])
                .With(x => x.IsActive, true)
                .Create();

            partnersRepositoryMock
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(partner);

            var request = fixture
                .Build<SetPartnerPromoCodeLimitRequest>()
                .Create();

            // Act
            _ = await this.partnersController.SetPartnerPromoCodeLimitAsync(default, request);

            // Assert
            partner.Should().NotBeNull();
            partner.PartnerLimits.First()!.CancelDate.Should().NotBeNull();

            partnersRepositoryMock.Verify(s => s.GetByIdAsync(default), Times.Once);
            partnersRepositoryMock.Verify(s => s.UpdateAsync(partner), Times.Once);
        }

        /// <summary>
        /// Тест на лимит должен больше 0.
        /// </summary>
        /// <returns> Асинхронная выполнение. </returns>
        [Fact]
        public async Task RequestLimitIsZero_BadRequestWithMessage_Success()
        {
            // Arrange
            var partnerPromoCodeLimit = fixture
                .Build<PartnerPromoCodeLimit>()
                .OmitAutoProperties()
                .Create();

            var partner = fixture.Build<Partner>()
                .OmitAutoProperties()
                .With(x => x.PartnerLimits, new List<PartnerPromoCodeLimit>() { partnerPromoCodeLimit })
                .Create();

            var request = fixture
                .Build<SetPartnerPromoCodeLimitRequest>()
                .OmitAutoProperties()
                .With(x => x.Limit, 0)
                .Create();

            partnersRepositoryMock
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(partner);

            // Act
            var result = await this.partnersController.SetPartnerPromoCodeLimitAsync(Guid.NewGuid(), request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should();
            result.Should().BeAssignableTo<BadRequestObjectResult>();
            partnersRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Once);
            partnersRepositoryMock.Verify(x => x.UpdateAsync(partner), Times.Never);
        }

        /// <summary>
        /// Тест на сохранение нового лимита.
        /// </summary>
        /// <returns> Асинхронная выполнение. </returns>
        [Fact]
        public async Task SaveNewLimitInDb_SaveLimitInDB_Success()
        {
            var partnerId = Guid.NewGuid();

            var dataContext = new Mock<DataContext>();

            var partnerPromoCodeLimit = fixture
                .Build<PartnerPromoCodeLimit>()
                .OmitAutoProperties()
                .Create();

            var partner = fixture
                .Build<Partner>()
                .OmitAutoProperties()
                .With(x => x.Id, partnerId)
                .With(x => x.IsActive, true)
                .With(x => x.PartnerLimits, new List<PartnerPromoCodeLimit>() { partnerPromoCodeLimit })
                .Create();

            var setPartnerPromoCodeLimitRequest = this.fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .OmitAutoProperties()
                .With(x => x.EndDate, DateTime.Now)
            .With(x => x.Limit, 10)
            .Create();

            partnersRepositoryMock
                .Setup(x => x.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            // Act
            var result = await partnersController.SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            partnersRepositoryMock.Verify(x => x.UpdateAsync(partner), Times.Once);
        }
    }
}