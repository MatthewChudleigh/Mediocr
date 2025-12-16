namespace Mediocr.Interfaces;

// Mediocr - a minimal replacement for MediatR
// Not great, not terrible

public interface IRequest<TOutput>
{

}

public interface IRequestHandler<in TInput, TOutput>
{
	Task<TOutput> Handle(TInput input, CancellationToken cancel);
}
