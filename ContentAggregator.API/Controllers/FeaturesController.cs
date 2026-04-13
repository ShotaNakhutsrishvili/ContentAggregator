using ContentAggregator.API.Contracts.Features;
using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ContentAggregator.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeaturesController : ControllerBase
    {
        private readonly IFeatureService _featureService;

        public FeaturesController(IFeatureService featureService)
        {
            _featureService = featureService;
        }

        // GET: api/Features
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<FeatureResponse>>> GetFeatures(CancellationToken cancellationToken)
        {
            var result = await _featureService.GetAllAsync(cancellationToken);

            return Ok(result.Select(MapToResponse).ToList());
        }

        // GET: api/Features/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<FeatureResponse>> GetFeature(int id, CancellationToken cancellationToken)
        {
            var feature = await _featureService.GetByIdAsync(id, cancellationToken);

            if (feature == null)
            {
                return NotFound();
            }

            return Ok(MapToResponse(feature));
        }

        // PUT: api/Features/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754 #TODO: Check this auto-generated link
        [HttpPut("{id:int}")]
        public async Task<ActionResult<FeatureResponse>> PutFeature(
            int id,
            [FromHeader(Name = "Prefer")] string? preferHeader,
            [FromBody] FeatureRequest request,
            CancellationToken cancellationToken)
        {
            var existingFeature = await _featureService.UpdateAsync(
                id,
                request.FirstNameEng,
                request.LastNameEng,
                request.FirstNameGeo,
                request.LastNameGeo,
                cancellationToken);
            if (existingFeature == null)
            {
                return NotFound();
            }

            bool wantsMinimalResponse = preferHeader?.Contains("return=minimal") ?? false;

            return wantsMinimalResponse ? NoContent() : Ok(MapToResponse(existingFeature));
        }

        // POST: api/Features
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        //[ServiceFilter(typeof(ValidateModelFilter))]
        //[ValidateModelFilter]
        public async Task<ActionResult<FeatureResponse>> PostFeature(
            [FromHeader(Name = "Prefer")] string? preferHeader,
            [FromBody] FeatureRequest request,
            CancellationToken cancellationToken)
        {
            var feature = await _featureService.CreateAsync(
                request.FirstNameEng,
                request.LastNameEng,
                request.FirstNameGeo,
                request.LastNameGeo,
                cancellationToken);

            bool wantsMinimalResponse = preferHeader?.Contains("return=minimal") ?? false;
            var response = MapToResponse(feature);

            return wantsMinimalResponse
                ? NoContent()
                : CreatedAtAction(nameof(GetFeature), new { id = response.Id }, response);
        }

        // DELETE: api/Features/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteFeature(int id, CancellationToken cancellationToken)
        {
            var result = await _featureService.DeleteAsync(id, cancellationToken);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private static FeatureResponse MapToResponse(Feature feature)
        {
            return new FeatureResponse(
                feature.Id,
                feature.FirstNameEng,
                feature.LastNameEng,
                feature.FirstNameGeo,
                feature.LastNameGeo,
                feature.CreatedAt,
                feature.UpdatedAt);
        }
    }
}
