using System;
using System.Data.Entity;

namespace smartlunch_api.Models
{
    public partial class DataContext : DbContext
    {
        public DataContext()
            : base("name=DataContext")
        {
        }

        /// <summary>
        /// Constructor con cadena de conexi�n (para setup inicial contra una BD concreta).
        /// </summary>
        public DataContext(string connectionString)
            : base(connectionString)
        {
        }

        // ==========================
        // DbSets
        // ==========================
        public virtual DbSet<sl_centrodecosto> sl_centrodecosto { get; set; }
        public virtual DbSet<sl_comanda> sl_comanda { get; set; }
        public virtual DbSet<sl_configuracion_> sl_configuracion { get; set; }
        public virtual DbSet<sl_jerarquia> sl_jerarquia { get; set; }
        public virtual DbSet<sl_menudd> sl_menudd { get; set; }
        public virtual DbSet<sl_plannutricional> sl_plannutricional { get; set; }
        public virtual DbSet<sl_planta> sl_planta { get; set; }
        public virtual DbSet<sl_plato> sl_plato { get; set; }
        public virtual DbSet<sl_proyecto> sl_proyecto { get; set; }
        public virtual DbSet<sl_regla_bonificacion> sl_regla_bonificacion { get; set; }
        public virtual DbSet<sl_turno> sl_turno { get; set; }
        public virtual DbSet<sl_usuario> sl_usuario { get; set; }
        public virtual DbSet<sl_tokenFichada> sl_tokenFichada { get; set; }
        public virtual DbSet<sl_login> sl_login { get; set; }
        public virtual DbSet<sl_refresh_token> sl_refresh_token { get; set; }
        public virtual DbSet<sl_datosfichada> sl_datosfichada { get; set; }
        public virtual DbSet<sl_configtotem> sl_configtotem { get; set; }
        public virtual DbSet<sl_fichada> sl_fichada { get; set; }
        public virtual DbSet<sl_fichada_smarttime> sl_fichada_smarttime { get; set; }

        // ==========================
        // Fluent API
        // ==========================
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Relaci�n sl_login <-> sl_usuario ----------
            // 1 usuario -> N logins (a nivel modelo)
            modelBuilder.Entity<sl_login>()
                .HasRequired(l => l.Usuario)
                .WithMany(u => u.Logins)
                .HasForeignKey(l => l.usuario_id)
                .WillCascadeOnDelete(false);

            // ---------- sl_comanda ----------
            modelBuilder.Entity<sl_comanda>()
                .Property(e => e.comentario)
                .IsUnicode(false);

            modelBuilder.Entity<sl_comanda>()
                .Property(e => e.monto)
                .HasPrecision(19, 4);

            modelBuilder.Entity<sl_comanda>()
                .Property(e => e.costo_proveedor)
                .HasPrecision(19, 4);

            // ---------- sl_configuracion ----------
            modelBuilder.Entity<sl_configuracion_>()
                .Property(e => e.parametro)
                .IsFixedLength();

            modelBuilder.Entity<sl_configuracion_>()
                .Property(e => e.valor)
                .IsFixedLength();

            // ---------- sl_plato ----------
            modelBuilder.Entity<sl_plato>()
                .Property(e => e.costo)
                .HasPrecision(19, 4);

            modelBuilder.Entity<sl_plato>()
                .Property(e => e.costo_proveedor)
                .HasPrecision(19, 4);

            // ---------- sl_regla_bonificacion ----------
            modelBuilder.Entity<sl_regla_bonificacion>()
                .Property(e => e.valor_efecto)
                .HasPrecision(19, 4);

            // Productos a los que aplica la regla (vacío = todos). Tabla puente propia,
            // sin PK identity, siguiendo el patrón estándar de EF6 para muchos-a-muchos.
            modelBuilder.Entity<sl_regla_bonificacion>()
                .HasMany(r => r.Platos)
                .WithMany()
                .Map(m =>
                {
                    m.ToTable("sl_regla_bonificacion_plato");
                    m.MapLeftKey("regla_bonificacion_id");
                    m.MapRightKey("plato_id");
                });

            // Turnos a los que aplica la regla (vacío = todos).
            modelBuilder.Entity<sl_regla_bonificacion>()
                .HasMany(r => r.Turnos)
                .WithMany()
                .Map(m =>
                {
                    m.ToTable("sl_regla_bonificacion_turno");
                    m.MapLeftKey("regla_bonificacion_id");
                    m.MapRightKey("turno_id");
                });

            // ---------- sl_planta ----------
            modelBuilder.Entity<sl_planta>()
                .Property(p => p.nombre)
                .IsUnicode(false);

            modelBuilder.Entity<sl_planta>()
                .Property(p => p.descripcion)
                .IsUnicode(false);

            // ---------- sl_login ----------
            modelBuilder.Entity<sl_login>()
                .ToTable("sl_login");

            modelBuilder.Entity<sl_login>()
                .Property(l => l.username)
                .IsUnicode(false)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<sl_login>()
                .Property(l => l.password_salt)
                .HasColumnType("varbinary")
                .HasMaxLength(16)
                .IsRequired();

            modelBuilder.Entity<sl_login>()
                .Property(l => l.password_hash)
                .HasColumnType("varbinary")
                .HasMaxLength(32)
                .IsRequired();

            modelBuilder.Entity<sl_login>()
                .Property(l => l.activo)
                .IsRequired();

            modelBuilder.Entity<sl_login>()
                .Property(l => l.deletemark)
                .IsRequired();

            modelBuilder.Entity<sl_refresh_token>()
                .HasRequired(r => r.Login)
                .WithMany()
                .HasForeignKey(r => r.login_id)
                .WillCascadeOnDelete(false);

            // ---------- Relaci�n sl_usuario � sl_jerarquia ----------
            // (si jerarquia_id es nullable -> HasOptional)
            modelBuilder.Entity<sl_usuario>()
                .HasOptional(u => u.jerarquia)
                .WithMany(j => j.usuarios)
                .HasForeignKey(u => u.jerarquia_id)
                .WillCascadeOnDelete(false);

            // ---------- Relaciones sl_menudd ----------
            // Turno
            modelBuilder.Entity<sl_menudd>()
                .HasRequired(m => m.Turno)
                .WithMany(t => t.menuesDelDia)
                .HasForeignKey(m => m.turno_id)
                .WillCascadeOnDelete(false);

            // Plato
            modelBuilder.Entity<sl_menudd>()
                .HasRequired(m => m.Plato)
                .WithMany(p => p.menuesDelDia)
                .HasForeignKey(m => m.plato_id)
                .WillCascadeOnDelete(false);

            // Planta
            modelBuilder.Entity<sl_menudd>()
                .HasRequired(m => m.Planta)
                .WithMany(p => p.menuesDelDia)
                .HasForeignKey(m => m.planta_id)
                .WillCascadeOnDelete(false);

            // Centro de costo
            modelBuilder.Entity<sl_menudd>()
                .HasRequired(m => m.CentroDeCosto)
                .WithMany(c => c.menuesDelDia)
                .HasForeignKey(m => m.centrodecosto_id)
                .WillCascadeOnDelete(false);

            // Proyecto
            modelBuilder.Entity<sl_menudd>()
                .HasRequired(m => m.Proyecto)
                .WithMany(p => p.menuesDelDia)
                .HasForeignKey(m => m.proyecto_id)
                .WillCascadeOnDelete(false);

            // Jerarqu�a (nullable)
            modelBuilder.Entity<sl_menudd>()
                .HasOptional(m => m.Jerarquia)
                .WithMany(j => j.menuesDelDia)
                .HasForeignKey(m => m.jerarquia_id)
                .WillCascadeOnDelete(false);
        }
    }
}
