using Microsoft.Extensions.Logging;

namespace LoginZju;

/// <summary>
/// Factory for creating per-user ZJUAM authentication and service instances.
/// Use this in multi-user scenarios (e.g. server applications) where each user
/// needs their own isolated session with separate cookies and state.
/// </summary>
public interface ILoginZjuFactory
{
    /// <summary>
    /// Creates a new <see cref="IZjuamAuth"/> instance for the specified user.
    /// </summary>
    /// <param name="username">ZJU unified identity username.</param>
    /// <param name="password">ZJU unified identity password.</param>
    IZjuamAuth CreateAuth(string username, string password);

    /// <summary>
    /// Creates a new <see cref="ICoursesService"/> instance bound to the given auth session.
    /// </summary>
    ICoursesService CreateCourses(IZjuamAuth auth);

    /// <summary>
    /// Creates a new <see cref="IZdbkService"/> instance bound to the given auth session.
    /// </summary>
    IZdbkService CreateZdbk(IZjuamAuth auth);

    /// <summary>
    /// Creates a new <see cref="IClassroomService"/> instance bound to the given auth session.
    /// </summary>
    IClassroomService CreateClassroom(IZjuamAuth auth);

    /// <summary>
    /// Creates a new <see cref="IFormService"/> instance bound to the given auth session.
    /// </summary>
    IFormService CreateForm(IZjuamAuth auth);

    /// <summary>
    /// Creates a new <see cref="IYqfkglService"/> instance bound to the given auth session.
    /// </summary>
    IYqfkglService CreateYqfkgl(IZjuamAuth auth);

    /// <summary>
    /// Creates a new <see cref="IOpenService"/> instance bound to the given auth session.
    /// </summary>
    IOpenService CreateOpen(IZjuamAuth auth);

    /// <summary>
    /// Creates a new <see cref="IEtaService"/> instance bound to the given auth session.
    /// </summary>
    IEtaService CreateEta(IZjuamAuth auth);
}


/// <inheritdoc cref="ILoginZjuFactory" />
public sealed class LoginZjuFactory : ILoginZjuFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="LoginZjuFactory"/>.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating typed loggers.</param>
    public LoginZjuFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IZjuamAuth CreateAuth(string username, string password)
        => new ZjuamAuth(username, password, _loggerFactory.CreateLogger<ZjuamAuth>());

    /// <inheritdoc />
    public ICoursesService CreateCourses(IZjuamAuth auth)
        => new CoursesService(auth, _loggerFactory.CreateLogger<CoursesService>());

    /// <inheritdoc />
    public IZdbkService CreateZdbk(IZjuamAuth auth)
        => new ZdbkService(auth, _loggerFactory.CreateLogger<ZdbkService>());

    /// <inheritdoc />
    public IClassroomService CreateClassroom(IZjuamAuth auth)
        => new ClassroomService(auth, _loggerFactory.CreateLogger<ClassroomService>());

    /// <inheritdoc />
    public IFormService CreateForm(IZjuamAuth auth)
        => new FormService(auth, _loggerFactory.CreateLogger<FormService>());

    /// <inheritdoc />
    public IYqfkglService CreateYqfkgl(IZjuamAuth auth)
        => new YqfkglService(auth, _loggerFactory.CreateLogger<YqfkglService>());

    /// <inheritdoc />
    public IOpenService CreateOpen(IZjuamAuth auth)
        => new OpenService(auth, _loggerFactory.CreateLogger<OpenService>());

    /// <inheritdoc />
    public IEtaService CreateEta(IZjuamAuth auth)
        => new EtaService(auth, _loggerFactory.CreateLogger<EtaService>());
}
