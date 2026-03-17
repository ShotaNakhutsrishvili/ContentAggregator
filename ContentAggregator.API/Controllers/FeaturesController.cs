using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ContentAggregator.Core.Interfaces;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models.DTOs;

namespace ContentAggregator.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeaturesController : ControllerBase
    {
        private readonly IFeatureRepository _featureRepository;

        public FeaturesController(IFeatureRepository featureRepository)
        {
            _featureRepository = featureRepository;
        }

        // GET: api/Features
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Feature>>> GetFeatures(CancellationToken cancellationToken)
        {
            var result = await _featureRepository.GetAllFeaturesAsync(cancellationToken);

            return Ok(result);
        }

        // GET: api/Features/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Feature>> GetFeature(int id, CancellationToken cancellationToken)
        {
            var feature = await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);

            if (feature == null)
            {
                return NotFound();
            }

            return Ok(feature);
        }

        // PUT: api/Features/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754 #TODO: Check this auto-generated link
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFeature(int id, [FromHeader(Name = "Prefer")] string? preferHeader, [FromBody] FeatureDto feature, CancellationToken cancellationToken)
        {
            var existingFeature = await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);
            if (existingFeature == null)
            {
                return NotFound();
            }

            // TODO: Use automapper to update only the specific properties
            existingFeature.FirstNameEng = feature.FirstNameEng;
            existingFeature.LastNameEng = feature.LastNameEng;
            existingFeature.FirstNameGeo = feature.FirstNameGeo;
            existingFeature.LastNameGeo = feature.LastNameGeo;

            existingFeature.UpdatedAt = DateTimeOffset.UtcNow;

            await _featureRepository.SaveChangesAsync(cancellationToken);

            bool wantsMinimalResponse = preferHeader?.Contains("return=minimal") ?? false;

            return wantsMinimalResponse ? NoContent() : Ok(existingFeature);
        }

        // POST: api/Features
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        //[ServiceFilter(typeof(ValidateModelFilter))]
        //[ValidateModelFilter]
        public async Task<ActionResult<Feature>> PostFeature([FromHeader(Name = "Prefer")] string? preferHeader, [FromBody] FeatureDto featureDto, CancellationToken cancellationToken)
        {
            Feature featureEntity = new Feature // TODO: Implement automapper
            {
                FirstNameEng = featureDto.FirstNameEng,
                LastNameEng = featureDto.LastNameEng,
                FirstNameGeo = featureDto.FirstNameGeo,
                LastNameGeo = featureDto.LastNameGeo
            };
            await _featureRepository.AddFeatureAsync(featureEntity, cancellationToken);

            bool wantsMinimalResponse = preferHeader?.Contains("return=minimal") ?? false;

            return wantsMinimalResponse
                ? NoContent()
                : CreatedAtAction(nameof(GetFeature), new { id = featureEntity.Id }, featureEntity);
        }

        // DELETE: api/Features/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFeature(int id, CancellationToken cancellationToken)
        {
            var result = await _featureRepository.DeleteFeatureAsync(id, cancellationToken);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
